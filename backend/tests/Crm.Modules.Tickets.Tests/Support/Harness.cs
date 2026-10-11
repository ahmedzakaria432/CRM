using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Support;

/// <summary>The doubles every Tickets handler needs, with one call log so a test can assert order: "rowversion", "add",
/// "save", "lookup" (any read of another module or of the categories repository) and "publish" are appended as they
/// happen. The module APIs answer permissively by default (no users, no customers, nothing found).</summary>
public sealed class Harness
{
    public Harness(DateTimeOffset? now = null, ICurrentUserService? user = null)
    {
        Tickets = Substitute.For<IRepository<Ticket, TicketId>>();
        Categories = Substitute.For<IRepository<Category, CategoryId>>();
        UnitOfWork = Substitute.For<ITicketsUnitOfWork>();
        Clock = Support.Clock.Fixed(now);
        Bus = Stubs.RecordingBus(Published);
        User = user ?? CurrentUsers.Agent(Scopes.AgentA1());
        Customers = Substitute.For<ICustomersModuleApi>();
        Identity = Substitute.For<IIdentityModuleApi>();
        Organisation = Substitute.For<IOrganisationModuleApi>();
        Localization = FakeLocalization.Keys();
        Numbers = Stubs.Numbers();

        Tickets.When(r => r.SetOriginalRowVersion(Arg.Any<Ticket>(), Arg.Any<byte[]>())).Do(_ => Log.Add("rowversion"));
        Categories.When(r => r.SetOriginalRowVersion(Arg.Any<Category>(), Arg.Any<byte[]>())).Do(_ => Log.Add("rowversion"));
        Tickets.When(r => r.Add(Arg.Any<Ticket>())).Do(call =>
        {
            Log.Add("add");
            Added.Add(call.Arg<Ticket>());
        });
        Categories.When(r => r.Add(Arg.Any<Category>())).Do(call =>
        {
            Log.Add("add");
            AddedCategories.Add(call.Arg<Category>());
        });
        UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Log.Add("save");
            return Task.FromResult(1);
        });
        Bus.When(b => b.Publish(Arg.Any<IIntegrationEvent>())).Do(_ => Log.Add("publish"));

        Categories.When(r => r.GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>())).Do(_ => Log.Add("lookup"));
        Categories.When(r => r.ListAsync(Arg.Any<Specification<Category>>(), Arg.Any<CancellationToken>())).Do(_ => Log.Add("lookup"));
        Identity.When(i => i.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())).Do(_ => Log.Add("lookup"));
        Organisation.When(o => o.GetDepartmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())).Do(_ => Log.Add("lookup"));
        Customers.When(c => c.GetCustomerNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())).Do(_ => Log.Add("lookup"));

        KnowsStaff();
        Customers.GetCustomerNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<CustomerNameSummary>>([])));
        Organisation.GetDepartmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Result.Failure<DepartmentReference>(
                Error.NotFound("Organisation.DepartmentNotFound", $"Department '{call.Arg<Guid>()}' was not found."))));
        Log.Clear();
    }

    public IRepository<Ticket, TicketId> Tickets { get; }

    public IRepository<Category, CategoryId> Categories { get; }

    public ITicketsUnitOfWork UnitOfWork { get; }

    public IDateTime Clock { get; }

    public IEventBus Bus { get; }

    public ICurrentUserService User { get; }

    public ICustomersModuleApi Customers { get; }

    public IIdentityModuleApi Identity { get; }

    public IOrganisationModuleApi Organisation { get; }

    public ILocalizationService Localization { get; set; }

    public ITicketNumberGenerator Numbers { get; }

    public List<IIntegrationEvent> Published { get; } = [];

    public List<Ticket> Added { get; } = [];

    public List<Category> AddedCategories { get; } = [];

    public List<string> Log { get; } = [];

    public int Saves => Log.Count(entry => entry == "save");

    public TicketDtoAssembler Assembler() => new(Categories, Identity, Organisation, Customers);

    public PortalTicketMapper Mapper() => new(Categories);

    /// <summary>The ticket repository returns this ticket for its id; any other id finds nothing.</summary>
    public Harness Holding(Ticket ticket)
    {
        Quietly(() => Tickets.Holds(ticket));
        return this;
    }

    public Harness Holding(Category category)
    {
        Quietly(() => Categories.Holds(category));
        return this;
    }

    /// <summary>Identity answers with these users (only the ones whose id was asked for).</summary>
    public Harness KnowsStaff(params StaffUserSummary[] users)
    {
        Quietly(() => Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Result.Success<IReadOnlyList<StaffUserSummary>>(
                [.. users.Where(u => call.Arg<IReadOnlyCollection<Guid>>().Contains(u.Id))]))));
        return this;
    }

    /// <summary>Organisation knows these departments.</summary>
    public Harness KnowsDepartments(params DepartmentReference[] departments)
    {
        Quietly(() => Organisation.GetDepartmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(departments.FirstOrDefault(d => d.Id == call.Arg<Guid>()) is { } found
                ? Result.Success(found)
                : Result.Failure<DepartmentReference>(Error.NotFound("Organisation.DepartmentNotFound", "unknown")))));
        return this;
    }

    /// <summary>Runs arrangement code and forgets the calls it made on the substitutes, so the log starts empty.</summary>
    public void Quietly(Action arrange)
    {
        ArgumentNullException.ThrowIfNull(arrange);

        int logged = Log.Count;
        arrange();
        Log.RemoveRange(logged, Log.Count - logged);
    }

    /// <summary>Nothing logged as a read or a save happens after the first publish: Publish is the last statement.</summary>
    public void AssertPublishIsLast()
    {
        int firstPublish = Log.IndexOf("publish");
        Assert.True(firstPublish >= 0, "nothing was published");
        Assert.DoesNotContain(Log.Skip(firstPublish), entry => entry is "lookup" or "save" or "add" or "rowversion");
    }

    /// <summary>Everything logged before the first save is a read or a row version; the save comes before every lookup of the assembler.</summary>
    public void AssertOrder(params string[] expected) =>
        Assert.Equal(expected, Log.Where(e => e is "rowversion" or "add" or "save" or "publish").ToList());
}

