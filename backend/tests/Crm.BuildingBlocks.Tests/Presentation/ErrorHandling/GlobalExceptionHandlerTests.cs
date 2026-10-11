using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Http;
using Crm.SharedKernel;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Crm.BuildingBlocks.Tests.Presentation.ErrorHandling;

public sealed class GlobalExceptionHandlerTests
{
    private sealed class Harness
    {
        public Harness(bool writeSucceeds = true)
        {
            Problems = new RecordingProblemDetailsService(writeSucceeds);
            var localization = Substitute.For<ILocalizationService>();
            localization.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());
            Logger = new RecordingLogger<GlobalExceptionHandler>();
            Handler = new GlobalExceptionHandler(Problems, Logger, localization);
        }

        public RecordingProblemDetailsService Problems { get; }

        public RecordingLogger<GlobalExceptionHandler> Logger { get; }

        public GlobalExceptionHandler Handler { get; }

        public List<ProblemDetailsContext> Written => Problems.Written;

        public async Task<(bool Handled, ProblemDetails Problem, DefaultHttpContext Context)> RunAsync(
            Exception exception, DefaultHttpContext? context = null)
        {
            context ??= new DefaultHttpContext();
            var handled = await Handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);
            return (handled, Written.Count > 0 ? Written[^1].ProblemDetails : new ProblemDetails(), context);
        }
    }

    [Fact]
    public async Task ADomainExceptionIsA422DomainRuleWithItsCodeAndMessage()
    {
        var harness = new Harness();

        var (handled, problem, context) = await harness.RunAsync(new DomainException(new DomainError("Ticket.Closed", "The ticket is closed.")));

        Assert.True(handled);
        Assert.Equal(422, problem.Status);
        Assert.Equal(ProblemTypes.DomainRule, problem.Type);
        Assert.Equal("The ticket is closed.", problem.Detail);
        Assert.Equal("Ticket.Closed", problem.Extensions["code"]);
        Assert.Equal(CrmMessageKeys.ProblemTitle.DomainRule, problem.Title);
        Assert.Equal(422, context.Response.StatusCode);
        Assert.True(harness.Logger.Contains(LogLevel.Information, "Ticket.Closed"));
    }

    [Fact]
    public async Task AValidationExceptionIsA400GroupedByProperty()
    {
        var harness = new Harness();
        var exception = new ValidationException(
        [
            new ValidationFailure("Name", "Name is required."),
            new ValidationFailure("Email", "Email is invalid."),
            new ValidationFailure("Name", "Name is too short."),
        ]);

        var (_, problem, context) = await harness.RunAsync(exception);

        var validation = Assert.IsType<ValidationProblemDetails>(problem);
        Assert.Equal(400, validation.Status);
        Assert.Equal(ProblemTypes.Validation, validation.Type);
        Assert.Equal(CrmMessageKeys.ProblemDetail.Validation, validation.Detail);
        Assert.Equal(CrmMessageKeys.ProblemTitle.Validation, validation.Title);
        Assert.Equal(["Name is required.", "Name is too short."], validation.Errors["Name"]);
        Assert.Equal(["Email is invalid."], validation.Errors["Email"]);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.True(harness.Logger.Contains(LogLevel.Information, "Validation"));
    }

    [Fact]
    public async Task AConcurrencyConflictIsA409WithItsCode()
    {
        var harness = new Harness();

        var (_, problem, _) = await harness.RunAsync(new ConcurrencyConflictException(new InvalidOperationException("stale")));

        Assert.Equal(409, problem.Status);
        Assert.Equal(ProblemTypes.Conflict, problem.Type);
        Assert.Equal("Persistence.ConcurrencyConflict", problem.Extensions["code"]);
        Assert.Equal(CrmMessageKeys.ProblemDetail.Concurrency, problem.Detail);
        Assert.True(harness.Logger.Contains(LogLevel.Warning, "Concurrency conflict"));
    }

    [Fact]
    public async Task AScopeViolationIsA403WithItsCode()
    {
        var harness = new Harness();

        var (_, problem, _) = await harness.RunAsync(new OrganisationScopeViolationException("Ticket", "Read"));

        Assert.Equal(403, problem.Status);
        Assert.Equal(ProblemTypes.Forbidden, problem.Type);
        Assert.Equal("Organisation.OutOfScope", problem.Extensions["code"]);
        Assert.Equal(CrmMessageKeys.ProblemDetail.OutOfScope, problem.Detail);
        Assert.True(harness.Logger.Contains(LogLevel.Warning, "Read Ticket"));
    }

    [Theory]
    [InlineData(415, "urn:crm:problem:unsupported-media-type")]
    [InlineData(400, "urn:crm:problem:bad-request")]
    [InlineData(413, "urn:crm:problem:bad-request")]
    public async Task AnUnreadableRequestKeepsItsStatusAndMapsItsType(int status, string type)
    {
        var harness = new Harness();

        var (_, problem, context) = await harness.RunAsync(new BadHttpRequestException("bad body", status));

        Assert.Equal(status, problem.Status);
        Assert.Equal(type, problem.Type);
        Assert.Equal(CrmMessageKeys.ProblemDetail.Unreadable, problem.Detail);
        Assert.False(problem.Extensions.ContainsKey("code"));
        Assert.Equal(status, context.Response.StatusCode);
        Assert.True(harness.Logger.Contains(LogLevel.Information, $"status {status}"));
    }

    public static TheoryData<string> DependencyFailures => new() { "http", "timeout", "circuit" };

    [Theory]
    [MemberData(nameof(DependencyFailures))]
    public async Task AFailedOutboundDependencyIsA503(string kind)
    {
        var harness = new Harness();
        Exception exception = kind switch
        {
            "http" => new HttpRequestException("connection refused"),
            "timeout" => new TimeoutRejectedException(TimeSpan.FromSeconds(1)),
            _ => new BrokenCircuitException(),
        };

        var (_, problem, context) = await harness.RunAsync(exception);

        Assert.Equal(503, problem.Status);
        Assert.Equal(ProblemTypes.DependencyUnavailable, problem.Type);
        Assert.Equal(CrmMessageKeys.ProblemDetail.DependencyUnavailable, problem.Detail);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Contains(harness.Logger.Entries, e => e.Level == LogLevel.Warning && ReferenceEquals(e.Exception, exception));
    }

    [Fact]
    public async Task AnyOtherExceptionIsA500ThatNeverCarriesTheExceptionText()
    {
        var harness = new Harness();
        var secret = "Server=prod-db;Password=hunter2";

        var (_, problem, context) = await harness.RunAsync(new InvalidOperationException(secret));

        Assert.Equal(500, problem.Status);
        Assert.Equal(ProblemTypes.Internal, problem.Type);
        Assert.Equal(CrmMessageKeys.ProblemDetail.Unexpected, problem.Detail);
        Assert.DoesNotContain("hunter2", problem.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", problem.Title ?? string.Empty, StringComparison.Ordinal);
        Assert.Empty(problem.Extensions);
        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task AnUnexpectedExceptionIsLoggedAtErrorWithTheException()
    {
        var harness = new Harness();
        var exception = new InvalidOperationException("boom");

        await harness.RunAsync(exception);

        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public async Task ACancellationAfterTheClientWentAwayIsLeftToTheFramework()
    {
        var harness = new Harness();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = source.Token };

        var (handled, _, _) = await harness.RunAsync(new OperationCanceledException(), context);

        Assert.False(handled);
        Assert.Empty(harness.Written);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Empty(harness.Logger.Entries);
    }

    [Fact]
    public async Task ACancellationWhileTheClientIsStillConnectedIsA500()
    {
        var harness = new Harness();

        var (handled, problem, _) = await harness.RunAsync(new OperationCanceledException("timeout inside the app"));

        Assert.True(handled);
        Assert.Equal(500, problem.Status);
    }

    [Fact]
    public async Task TheReturnValueIsWhateverTheProblemDetailsServiceReturns()
    {
        var harness = new Harness(writeSucceeds: false);

        var (handled, _, _) = await harness.RunAsync(new InvalidOperationException("x"));

        Assert.False(handled);
        Assert.Single(harness.Written);
    }

    [Fact]
    public async Task TheContextHandedToTheServiceCarriesTheHttpContextAndTheException()
    {
        var harness = new Harness();
        var exception = new DomainException(new DomainError("X", "y"));
        var context = new DefaultHttpContext();

        await harness.RunAsync(exception, context);

        var written = Assert.Single(harness.Written);
        Assert.Same(context, written.HttpContext);
        Assert.Same(exception, written.Exception);
    }
}
