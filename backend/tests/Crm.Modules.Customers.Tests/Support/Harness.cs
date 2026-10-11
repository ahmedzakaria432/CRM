using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Tests.Support;

/// <summary>The doubles every command handler needs, with one call log so a test can assert order:
/// "rowversion", "add", "save" and "publish" are appended as they happen.</summary>
public sealed class Harness
{
    public Harness(DateTimeOffset? now = null)
    {
        Repository = Substitute.For<IRepository<Customer, CustomerId>>();
        UnitOfWork = Substitute.For<ICustomersUnitOfWork>();
        Clock = Support.Clock.Fixed(now);
        Bus = Stubs.RecordingBus(Published);

        Repository.When(r => r.SetOriginalRowVersion(Arg.Any<Customer>(), Arg.Any<byte[]>())).Do(_ => Log.Add("rowversion"));
        Repository.When(r => r.Add(Arg.Any<Customer>())).Do(_ => Log.Add("add"));
        UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Log.Add("save");
            return Task.FromResult(1);
        });
        Bus.When(b => b.Publish(Arg.Any<IIntegrationEvent>())).Do(_ => Log.Add("publish"));
    }

    public IRepository<Customer, CustomerId> Repository { get; }

    public ICustomersUnitOfWork UnitOfWork { get; }

    public IDateTime Clock { get; }

    public IEventBus Bus { get; }

    public List<IIntegrationEvent> Published { get; } = [];

    public List<string> Log { get; } = [];

    public int Saves => Log.Count(entry => entry == "save");

    /// <summary>The repository returns this customer for its id; any other id finds nothing.</summary>
    public Harness Holding(Customer customer)
    {
        Repository.Holds(customer);
        return this;
    }
}
