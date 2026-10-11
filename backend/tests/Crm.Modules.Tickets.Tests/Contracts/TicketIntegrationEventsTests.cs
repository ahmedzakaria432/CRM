using System.Reflection;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Tests.Contracts;

/// <summary>Integration event records are append-only: an undelivered outbox row still holds the old shape and type
/// name, so a rename or removal of a member breaks a stored message. The shapes are pinned member by member.</summary>
public sealed class TicketIntegrationEventsTests
{
    private static readonly Guid EventId = new("e0e0e0e0-0000-0000-0000-000000000001");

    private static readonly Dictionary<Type, string[]> Shapes = new()
    {
        [typeof(TicketCreatedIntegrationEvent)] =
        [
            "Id:Guid", "OccurredOnUtc:DateTimeOffset", "TicketId:Guid", "Number:String", "CustomerId:Guid", "ContactId:Guid?", "Channel:String",
            "CategoryId:Guid?", "Priority:String", "Status:String", "AssignedAgentId:Guid?", "ActorId:Guid", "TenantId:Guid", "BranchId:Guid",
            "DepartmentId:Guid",
        ],
        [typeof(TicketStatusChangedIntegrationEvent)] =
        [
            "Id:Guid", "OccurredOnUtc:DateTimeOffset", "TicketId:Guid", "Number:String", "FromStatus:String", "ToStatus:String", "ActorId:Guid",
            "TenantId:Guid", "BranchId:Guid", "DepartmentId:Guid",
        ],
        [typeof(TicketAssignedIntegrationEvent)] =
        [
            "Id:Guid", "OccurredOnUtc:DateTimeOffset", "TicketId:Guid", "Number:String", "PreviousAgentId:Guid?", "AssignedAgentId:Guid?",
            "ActorId:Guid", "TenantId:Guid", "BranchId:Guid", "DepartmentId:Guid",
        ],
        [typeof(TicketTransferredIntegrationEvent)] =
        [
            "Id:Guid", "OccurredOnUtc:DateTimeOffset", "TicketId:Guid", "Number:String", "FromBranchId:Guid", "FromDepartmentId:Guid",
            "ToBranchId:Guid", "ToDepartmentId:Guid", "PreviousAgentId:Guid?", "ActorId:Guid", "TenantId:Guid",
        ],
        [typeof(TicketPriorityChangedIntegrationEvent)] =
        [
            "Id:Guid", "OccurredOnUtc:DateTimeOffset", "TicketId:Guid", "Number:String", "FromPriority:String", "ToPriority:String", "ActorId:Guid",
            "TenantId:Guid", "BranchId:Guid", "DepartmentId:Guid",
        ],
        [typeof(TicketCategoryChangedIntegrationEvent)] =
        [
            "Id:Guid", "OccurredOnUtc:DateTimeOffset", "TicketId:Guid", "Number:String", "FromCategoryId:Guid?", "ToCategoryId:Guid", "ActorId:Guid",
            "TenantId:Guid", "BranchId:Guid", "DepartmentId:Guid",
        ],
        [typeof(TicketMessageAddedIntegrationEvent)] =
        [
            "Id:Guid", "OccurredOnUtc:DateTimeOffset", "TicketId:Guid", "Number:String", "MessageId:Guid", "Direction:String", "AuthorId:Guid?",
            "CustomerId:Guid", "ContactId:Guid?", "IsFirstResponse:Boolean", "ActorId:Guid", "TenantId:Guid", "BranchId:Guid", "DepartmentId:Guid",
        ],
    };

    private static string Describe(ParameterInfo parameter)
    {
        Type type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        string nullable = Nullable.GetUnderlyingType(parameter.ParameterType) is not null ? "?" : string.Empty;
        return $"{parameter.Name}:{type.Name}{nullable}";
    }

    [Fact]
    public void ThereAreSevenEventRecords()
    {
        Type[] events = [.. typeof(TicketCreatedIntegrationEvent).Assembly.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && t.IsClass)];

        Assert.Equal(7, events.Length);
        Assert.Equal(Shapes.Keys.Select(k => k.Name).Order(StringComparer.Ordinal), events.Select(t => t.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryRecordKeepsItsExactOrderedMembers()
    {
        foreach ((Type type, string[] expected) in Shapes)
        {
            ConstructorInfo constructor = Assert.Single(type.GetConstructors());

            Assert.True(
                expected.SequenceEqual(constructor.GetParameters().Select(Describe)),
                $"{type.Name}: {string.Join(", ", constructor.GetParameters().Select(Describe))}");
        }
    }

    [Fact]
    public void EveryRecordDerivesFromIntegrationEventAndIsSealed()
    {
        foreach (Type type in Shapes.Keys)
        {
            Assert.True(typeof(IntegrationEvent).IsAssignableFrom(type), type.Name);
            Assert.True(typeof(IIntegrationEvent).IsAssignableFrom(type), type.Name);
            Assert.True(type.IsSealed, type.Name);
        }
    }

    [Fact]
    public void TheMessageAddedEventHasNoBodyMember()
    {
        Assert.Null(typeof(TicketMessageAddedIntegrationEvent).GetProperty("Body"));
        Assert.DoesNotContain(
            typeof(TicketMessageAddedIntegrationEvent).GetConstructors().Single().GetParameters(),
            p => p.Name is not null && p.Name.Contains("Body", StringComparison.Ordinal));
    }

    [Fact]
    public void ARecordCarriesItsConstructorArguments()
    {
        var created = new TicketCreatedIntegrationEvent(
            EventId, Clock.Now, Ids.Ticket1, "T-000001", Ids.Customer1, Ids.Contact1, "Agent", Ids.Category1, "High", "New", Ids.Agent1, Ids.Actor,
            Ids.TenantA.Value, Ids.BranchA1.Value, Ids.DepartmentA1.Value);

        Assert.Equal(EventId, created.Id);
        Assert.Equal(Clock.Now, created.OccurredOnUtc);
        Assert.Equal(Ids.Ticket1, created.TicketId);
        Assert.Equal("T-000001", created.Number);
        Assert.Equal(Ids.Customer1, created.CustomerId);
        Assert.Equal(Ids.Contact1, created.ContactId);
        Assert.Equal("Agent", created.Channel);
        Assert.Equal(Ids.Category1, created.CategoryId);
        Assert.Equal("High", created.Priority);
        Assert.Equal("New", created.Status);
        Assert.Equal(Ids.Agent1, created.AssignedAgentId);
        Assert.Equal(Ids.Actor, created.ActorId);
        Assert.Equal(Ids.TenantA.Value, created.TenantId);
        Assert.Equal(Ids.BranchA1.Value, created.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, created.DepartmentId);
    }

    [Fact]
    public void RecordsCompareByValueAndSupportWith()
    {
        var changed = new TicketStatusChangedIntegrationEvent(
            EventId, Clock.Now, Ids.Ticket1, "T-000001", "New", "Open", Ids.Actor, Ids.TenantA.Value, Ids.BranchA1.Value, Ids.DepartmentA1.Value);
        TicketStatusChangedIntegrationEvent same = changed with { };
        TicketStatusChangedIntegrationEvent other = changed with { ToStatus = "Resolved" };

        Assert.Equal(changed, same);
        Assert.NotEqual(changed, other);
        Assert.Equal("Resolved", other.ToStatus);
        Assert.Equal("Open", changed.ToStatus);
    }

    [Fact]
    public void TheTransferredEventCarriesBeforeAndAfterPlaces()
    {
        var transferred = new TicketTransferredIntegrationEvent(
            EventId, Clock.Now, Ids.Ticket1, "T-000001", Ids.BranchA1.Value, Ids.DepartmentA1.Value, Ids.BranchA2.Value, Ids.DepartmentA2.Value,
            Ids.Agent1, Ids.Actor, Ids.TenantA.Value);

        Assert.Equal(Ids.BranchA1.Value, transferred.FromBranchId);
        Assert.Equal(Ids.DepartmentA2.Value, transferred.ToDepartmentId);
        Assert.Equal(Ids.Agent1, transferred.PreviousAgentId);
    }

    [Fact]
    public void ThePublicContractsAssemblyExposesNoModuleApiInterface()
    {
        Type[] publicTypes = [.. typeof(TicketCreatedIntegrationEvent).Assembly.GetExportedTypes()];

        Assert.DoesNotContain(publicTypes, t => t.IsInterface && t.Name.EndsWith("ModuleApi", StringComparison.Ordinal));
    }
}
