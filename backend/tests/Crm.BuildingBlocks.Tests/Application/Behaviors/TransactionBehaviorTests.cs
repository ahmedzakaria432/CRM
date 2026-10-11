using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Behaviors;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Tests.Application.Behaviors;

public sealed class TransactionBehaviorTests
{
    private const string CorrelationId = "corr-123";

    private sealed class Harness<TRequest, TResponse>
        where TRequest : notnull
    {
        public Harness()
        {
            UnitOfWork = Substitute.For<ITestUnitOfWork>();
            Resolver = Substitute.For<IUnitOfWorkResolver>();
            Resolver.ForRequest(Arg.Any<Type>()).Returns(UnitOfWork);
            Dispatcher = Substitute.For<IDomainEventDispatcher>();
            Correlation = Substitute.For<ICorrelationIdAccessor>();
            Correlation.CorrelationId.Returns(CorrelationId);
            Cache = Substitute.For<ICacheService>();
            Logger = new RecordingLogger<TransactionBehavior<TRequest, TResponse>>();
            Bus = new OutboxEventBus();
            Behavior = new TransactionBehavior<TRequest, TResponse>(Resolver, Dispatcher, Bus, Correlation, Cache, Logger);

            UnitOfWork.GetAndClearDomainEvents().Returns(DomainEvents);
            UnitOfWork.When(u => u.BeginTransactionAsync(Arg.Any<CancellationToken>())).Do(_ => Order.Add("begin"));
            UnitOfWork.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>())).Do(_ => Order.Add("save"));
            UnitOfWork.When(u => u.AddToOutbox(Arg.Any<IReadOnlyCollection<IIntegrationEvent>>(), Arg.Any<string?>())).Do(_ => Order.Add("outbox"));
            UnitOfWork.When(u => u.GetAndClearDomainEvents()).Do(_ => Order.Add("harvest"));
            UnitOfWork.When(u => u.CommitTransactionAsync(Arg.Any<CancellationToken>())).Do(_ => Order.Add("commit"));
            UnitOfWork.When(u => u.RollbackTransactionAsync(Arg.Any<CancellationToken>())).Do(_ => Order.Add("rollback"));
            Cache.When(c => c.InvalidateTagsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())).Do(_ => Order.Add("invalidate"));
            Dispatcher.When(d => d.DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>())).Do(_ => Order.Add("dispatch"));
        }

        public ITestUnitOfWork UnitOfWork { get; }

        public IUnitOfWorkResolver Resolver { get; }

        public IDomainEventDispatcher Dispatcher { get; }

        public ICorrelationIdAccessor Correlation { get; }

        public ICacheService Cache { get; }

        public RecordingLogger<TransactionBehavior<TRequest, TResponse>> Logger { get; }

        public OutboxEventBus Bus { get; }

        public TransactionBehavior<TRequest, TResponse> Behavior { get; }

        public List<string> Order { get; } = [];

        public IReadOnlyCollection<IDomainEvent> DomainEvents { get; } = new List<IDomainEvent> { new SampleDomainEvent(Clock.Now) };

        public Task<TResponse> Run(TRequest request, RequestHandlerDelegateProbe<TResponse> next) =>
            Behavior.Handle(request, next.Next, TestContext.Current.CancellationToken);
    }

    /// <summary>Wraps a body so it can record "handler" in the harness order list.</summary>
    private sealed class RequestHandlerDelegateProbe<TResponse>(Func<Task<TResponse>> body)
    {
        public MediatR.RequestHandlerDelegate<TResponse> Next => () => body();
    }

    private static SampleIntegrationEvent Event(string payload = "p") => new(Guid.NewGuid(), Clock.Now, payload);

    // --- queries ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AQueryOpensNoTransactionAndPassesThrough()
    {
        var harness = new Harness<ReadQuery, Result<int>>();
        var response = Result.Success(9);

        var result = await harness.Run(new ReadQuery(), new RequestHandlerDelegateProbe<Result<int>>(() => Task.FromResult(response)));

        Assert.Same(response, result);
        harness.Resolver.DidNotReceive().ForRequest(Arg.Any<Type>());
        await harness.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await harness.UnitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AQueryThatPublishedAnIntegrationEventFailsLoudlyAndDrainsTheQueue()
    {
        var harness = new Harness<ReadQuery, Result<int>>();
        var probe = new RequestHandlerDelegateProbe<Result<int>>(() =>
        {
            harness.Bus.Publish(Event());
            return Task.FromResult(Result.Success(1));
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Run(new ReadQuery(), probe));

        Assert.Contains("a query", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ReadQuery", exception.Message, StringComparison.Ordinal);
        Assert.False(harness.Bus.HasPending);
    }

    // --- commands: success --------------------------------------------------------------------------------

    [Fact]
    public async Task ACommandResolvesTheUnitOfWorkOfItsOwnType()
    {
        var harness = new Harness<WriteCommand, Result>();

        await harness.Run(new WriteCommand(), new RequestHandlerDelegateProbe<Result>(() => Task.FromResult(Result.Success())));

        harness.Resolver.Received(1).ForRequest(typeof(WriteCommand));
    }

    [Fact]
    public async Task ACommandRunsBeginHandlerHarvestCommitInvalidateDispatchInThatOrder()
    {
        var harness = new Harness<InvalidatingCommand, Result>();
        var probe = new RequestHandlerDelegateProbe<Result>(() =>
        {
            harness.Order.Add("handler");
            return Task.FromResult(Result.Success());
        });

        var result = await harness.Run(new InvalidatingCommand(), probe);

        Assert.True(result.IsSuccess);
        Assert.Equal(["begin", "handler", "harvest", "commit", "invalidate", "dispatch"], harness.Order);
    }

    [Fact]
    public async Task TheHarvestedDomainEventsAreWhatGetsDispatched()
    {
        var harness = new Harness<WriteCommand, Result>();

        await harness.Run(new WriteCommand(), new RequestHandlerDelegateProbe<Result>(() => Task.FromResult(Result.Success())));

        await harness.Dispatcher.Received(1).DispatchAsync(harness.DomainEvents, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithNoIntegrationEventsNothingIsStagedAndThereIsNoEarlySave()
    {
        var harness = new Harness<WriteCommand, Result>();

        await harness.Run(new WriteCommand(), new RequestHandlerDelegateProbe<Result>(() => Task.FromResult(Result.Success())));

        harness.UnitOfWork.DidNotReceive().AddToOutbox(Arg.Any<IReadOnlyCollection<IIntegrationEvent>>(), Arg.Any<string?>());
        await harness.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IntegrationEventsAreStagedInTheOutboxAndSavedBeforeCommit()
    {
        var harness = new Harness<WriteCommand, Result>();
        var first = Event("a");
        var second = Event("b");
        var probe = new RequestHandlerDelegateProbe<Result>(() =>
        {
            harness.Bus.Publish(first);
            harness.Bus.Publish(second);
            return Task.FromResult(Result.Success());
        });

        await harness.Run(new WriteCommand(), probe);

        harness.UnitOfWork.Received(1).AddToOutbox(
            Arg.Is<IReadOnlyCollection<IIntegrationEvent>>(e => e.SequenceEqual(new IIntegrationEvent[] { first, second })),
            CorrelationId);
        Assert.Equal(["begin", "outbox", "save", "harvest", "commit", "dispatch"], harness.Order);
        Assert.False(harness.Bus.HasPending);
    }

    [Fact]
    public async Task ACommandWithoutInvalidationLeavesTheCacheAlone()
    {
        var harness = new Harness<WriteCommand, Result>();

        await harness.Run(new WriteCommand(), new RequestHandlerDelegateProbe<Result>(() => Task.FromResult(Result.Success())));

        await harness.Cache.DidNotReceive().InvalidateTagsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnInvalidatingCommandInvalidatesItsTagsAfterCommit()
    {
        var harness = new Harness<InvalidatingCommand, Result>();

        await harness.Run(new InvalidatingCommand(), new RequestHandlerDelegateProbe<Result>(() => Task.FromResult(Result.Success())));

        await harness.Cache.Received(1).InvalidateTagsAsync(
            Arg.Is<IReadOnlyCollection<string>>(t => t.SequenceEqual(new InvalidatingCommand().CacheTagsToInvalidate)),
            Arg.Any<CancellationToken>());
        Assert.True(harness.Order.IndexOf("invalidate") > harness.Order.IndexOf("commit"));
    }

    [Fact]
    public async Task ATypedCommandReturnsTheHandlersValue()
    {
        var harness = new Harness<ValueCommand, Result<int>>();

        var result = await harness.Run(new ValueCommand(), new RequestHandlerDelegateProbe<Result<int>>(() => Task.FromResult(Result.Success(11))));

        Assert.Equal(11, result.Value);
    }

    [Fact]
    public async Task TheCancellationTokenReachesTheUnitOfWork()
    {
        var harness = new Harness<WriteCommand, Result>();
        using var source = new CancellationTokenSource();

        await harness.Behavior.Handle(
            new WriteCommand(), new RequestHandlerDelegateProbe<Result>(() => Task.FromResult(Result.Success())).Next, source.Token);

        await harness.UnitOfWork.Received(1).BeginTransactionAsync(source.Token);
        await harness.UnitOfWork.Received(1).CommitTransactionAsync(source.Token);
    }

    // --- commands: failure --------------------------------------------------------------------------------

    [Fact]
    public async Task AFailedResultRollsBackAndNothingElseHappens()
    {
        var harness = new Harness<InvalidatingCommand, Result>();
        var failure = Result.Failure(Error.Conflict("X.Conflict", "no"));
        var probe = new RequestHandlerDelegateProbe<Result>(() =>
        {
            harness.Bus.Publish(Event());
            harness.Order.Add("handler");
            return Task.FromResult(failure);
        });

        var result = await harness.Run(new InvalidatingCommand(), probe);

        Assert.Same(failure, result);
        Assert.Equal(["begin", "handler", "rollback"], harness.Order);
        Assert.False(harness.Bus.HasPending);
        harness.UnitOfWork.DidNotReceive().AddToOutbox(Arg.Any<IReadOnlyCollection<IIntegrationEvent>>(), Arg.Any<string?>());
        await harness.UnitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
        await harness.Cache.DidNotReceive().InvalidateTagsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await harness.Dispatcher.DidNotReceive().DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AFailedTypedResultAlsoRollsBack()
    {
        var harness = new Harness<ValueCommand, Result<int>>();
        var failure = Result.Failure<int>(Error.Validation("V", "bad"));

        var result = await harness.Run(new ValueCommand(), new RequestHandlerDelegateProbe<Result<int>>(() => Task.FromResult(failure)));

        Assert.Same(failure, result);
        await harness.UnitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await harness.UnitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AConcurrencyConflictRollsBackDropsEventsAndBecomesAConflictResult()
    {
        var harness = new Harness<WriteCommand, Result>();
        var probe = new RequestHandlerDelegateProbe<Result>(() =>
        {
            harness.Bus.Publish(Event());
            throw new ConcurrencyConflictException(new InvalidOperationException("stale"));
        });

        var result = await harness.Run(new WriteCommand(), probe);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Persistence.ConcurrencyConflict", result.Error.Code);
        Assert.False(harness.Bus.HasPending);
        await harness.UnitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await harness.UnitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
        Assert.True(harness.Logger.Contains(LogLevel.Warning, "WriteCommand"));
    }

    [Fact]
    public async Task AConcurrencyConflictOnATypedCommandGivesATypedFailure()
    {
        var harness = new Harness<ValueCommand, Result<int>>();
        var probe = new RequestHandlerDelegateProbe<Result<int>>(
            () => throw new ConcurrencyConflictException(new InvalidOperationException("stale")));

        var result = await harness.Run(new ValueCommand(), probe);

        Assert.True(result.IsFailure);
        Assert.Equal("Persistence.ConcurrencyConflict", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public async Task AScopeViolationRollsBackDropsEventsAndIsRethrown()
    {
        var harness = new Harness<WriteCommand, Result>();
        var violation = new OrganisationScopeViolationException("Ticket", "Write");
        var probe = new RequestHandlerDelegateProbe<Result>(() =>
        {
            harness.Bus.Publish(Event());
            throw violation;
        });

        var thrown = await Assert.ThrowsAsync<OrganisationScopeViolationException>(() => harness.Run(new WriteCommand(), probe));

        Assert.Same(violation, thrown);
        Assert.False(harness.Bus.HasPending);
        await harness.UnitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await harness.UnitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnIntegrationEventPublishedFromADomainEventHandlerFailsLoudlyAndIsDropped()
    {
        var harness = new Harness<WriteCommand, Result>();
        harness.Dispatcher
            .When(d => d.DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>()))
            .Do(_ => harness.Bus.Publish(Event("late")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Run(new WriteCommand(), new RequestHandlerDelegateProbe<Result>(() => Task.FromResult(Result.Success()))));

        Assert.Contains("domain-event handler", exception.Message, StringComparison.Ordinal);
        Assert.False(harness.Bus.HasPending);
    }

    [Fact]
    public async Task AnyOtherExceptionFromTheHandlerPropagatesAndNeverCommits()
    {
        // Pins only what the code states: the exception propagates and the commit is never reached. The behaviour
        // does not call RollbackTransactionAsync for it (the unit of work is disposed with the request scope); that is
        // recorded as a Follow-up rather than asserted.
        var harness = new Harness<WriteCommand, Result>();
        var boom = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Run(new WriteCommand(), new RequestHandlerDelegateProbe<Result>(() => throw boom)));

        Assert.Same(boom, thrown);
        await harness.UnitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
        await harness.Dispatcher.DidNotReceive().DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANullNextDelegateIsRefused()
    {
        var harness = new Harness<WriteCommand, Result>();

        await NullArguments.AssertThrowsArgumentNullAsync(
            harness.Behavior.Handle, new WriteCommand(), null, TestContext.Current.CancellationToken);
    }
}
