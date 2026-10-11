using System.Reflection;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Contracts.DevSeed;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Tests.Contracts;

public sealed class CustomerIntegrationEventsTests
{
    private static readonly Guid EventId = new("e0e0e0e0-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset At = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = new("c1c1c1c1-0000-0000-0000-000000000001");
    private static readonly Guid Tenant = new("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid Branch = new("a1a1a1a1-0000-0000-0000-0000000000a1");
    private static readonly Guid Department = new("d1d1d1d1-0000-0000-0000-0000000000a1");

    private static CustomerCreatedIntegrationEvent Created() =>
        new(EventId, At, CustomerId, "C-000001", "أ", "A", "Individual", Tenant, Branch, Department);

    private static CustomerUpdatedIntegrationEvent Updated() =>
        new(EventId, At, CustomerId, "أ", "A", "Individual", "Active");

    private static (string Name, Type Type)[] Members(Type record) =>
    [
        .. record.GetConstructors().Single().GetParameters().Select(p => (p.Name ?? string.Empty, p.ParameterType)),
    ];

    [Fact]
    public void TheCreatedEventCarriesEveryArgument()
    {
        CustomerCreatedIntegrationEvent created = Created();

        Assert.Equal(EventId, created.Id);
        Assert.Equal(At, created.OccurredOnUtc);
        Assert.Equal(CustomerId, created.CustomerId);
        Assert.Equal("C-000001", created.Number);
        Assert.Equal("أ", created.DisplayNameAr);
        Assert.Equal("A", created.DisplayNameEn);
        Assert.Equal("Individual", created.Type);
        Assert.Equal(Tenant, created.TenantId);
        Assert.Equal(Branch, created.BranchId);
        Assert.Equal(Department, created.DepartmentId);
    }

    [Fact]
    public void TheUpdatedEventCarriesEveryArgument()
    {
        CustomerUpdatedIntegrationEvent updated = Updated();

        Assert.Equal(EventId, updated.Id);
        Assert.Equal(At, updated.OccurredOnUtc);
        Assert.Equal(CustomerId, updated.CustomerId);
        Assert.Equal("أ", updated.DisplayNameAr);
        Assert.Equal("A", updated.DisplayNameEn);
        Assert.Equal("Individual", updated.Type);
        Assert.Equal("Active", updated.Status);
    }

    [Fact]
    public void BothEventsAreIntegrationEvents()
    {
        Assert.IsAssignableFrom<IntegrationEvent>(Created());
        Assert.IsAssignableFrom<IIntegrationEvent>(Created());
        Assert.IsAssignableFrom<IntegrationEvent>(Updated());
        Assert.IsAssignableFrom<IIntegrationEvent>(Updated());
    }

    [Fact]
    public void TheEventsAreValueRecordsThatCanBeCopied()
    {
        Assert.Equal(Created(), Created());
        Assert.NotEqual(Created(), Created() with { Number = "C-000002" });
        Assert.Equal("C-000002", (Created() with { Number = "C-000002" }).Number);
        Assert.Equal(Updated(), Updated());
        Assert.NotEqual(Updated(), Updated() with { Status = "Inactive" });
    }

    [Fact]
    public void TheCreatedEventShapeIsAppendOnly() =>
        Assert.Equal(
            [
                ("Id", typeof(Guid)), ("OccurredOnUtc", typeof(DateTimeOffset)), ("CustomerId", typeof(Guid)), ("Number", typeof(string)),
                ("DisplayNameAr", typeof(string)), ("DisplayNameEn", typeof(string)), ("Type", typeof(string)), ("TenantId", typeof(Guid)),
                ("BranchId", typeof(Guid)), ("DepartmentId", typeof(Guid)),
            ],
            Members(typeof(CustomerCreatedIntegrationEvent)));

    [Fact]
    public void TheUpdatedEventShapeIsAppendOnly() =>
        Assert.Equal(
            [
                ("Id", typeof(Guid)), ("OccurredOnUtc", typeof(DateTimeOffset)), ("CustomerId", typeof(Guid)), ("DisplayNameAr", typeof(string)),
                ("DisplayNameEn", typeof(string)), ("Type", typeof(string)), ("Status", typeof(string)),
            ],
            Members(typeof(CustomerUpdatedIntegrationEvent)));
}

public sealed class ContractRecordsTests
{
    private static readonly Guid A = new("c1c1c1c1-0000-0000-0000-000000000001");
    private static readonly Guid B = new("c0c0c0c0-0000-0000-0000-000000000002");
    private static readonly Guid C = new("aaaaaaaa-0000-0000-0000-00000000000a");

    [Fact]
    public void APortalContactMatchCarriesItsArgumentsAndHasValueEquality()
    {
        var match = new PortalContactMatch(A, B, "Sara", "sara@example.com", true, "ar", C, C, C);

        Assert.Equal(A, match.CustomerId);
        Assert.Equal(B, match.ContactId);
        Assert.Equal("Sara", match.ContactName);
        Assert.Equal("sara@example.com", match.Email);
        Assert.True(match.IsPrimary);
        Assert.Equal("ar", match.PreferredLanguage);
        Assert.Equal(C, match.TenantId);
        Assert.Equal(C, match.BranchId);
        Assert.Equal(C, match.DepartmentId);
        Assert.Equal(match, new PortalContactMatch(A, B, "Sara", "sara@example.com", true, "ar", C, C, C));
        Assert.NotEqual(match, match with { IsPrimary = false });
    }

    [Fact]
    public void ACustomerSummaryCarriesItsArgumentsAndItsContacts()
    {
        CustomerContactSummary contact = new(B, "Sara", "sara@example.com", null, true, false);
        var summary = new CustomerSummary(A, "C-000001", "أ", "A", "Individual", true, "en", C, C, C, [contact]);

        Assert.Equal(A, summary.Id);
        Assert.Equal("C-000001", summary.Number);
        Assert.Equal("أ", summary.DisplayNameAr);
        Assert.Equal("A", summary.DisplayNameEn);
        Assert.Equal("Individual", summary.Type);
        Assert.True(summary.IsActive);
        Assert.Equal("en", summary.PreferredLanguage);
        Assert.Equal(contact, Assert.Single(summary.Contacts));
        Assert.Equal(new CustomerContactSummary(B, "Sara", "sara@example.com", null, true, false), contact);
        Assert.NotEqual(contact, contact with { HasPortalAccess = true });
    }

    [Fact]
    public void AContactMatchAndANameSummaryCarryTheirArgumentsAndHaveValueEquality()
    {
        var match = new CustomerContactMatch(A, "C-000001", true, B, "Sara", "sara@example.com", false, C, C, C);
        var name = new CustomerNameSummary(A, "C-000001", "أ", "A");

        Assert.Equal("C-000001", match.CustomerNumber);
        Assert.True(match.IsCustomerActive);
        Assert.False(match.HasPortalAccess);
        Assert.Equal(match, match with { ContactName = "Sara" });
        Assert.NotEqual(match, match with { IsCustomerActive = false });
        Assert.Equal("A", name.DisplayNameEn);
        Assert.Equal(new CustomerNameSummary(A, "C-000001", "أ", "A"), name);
    }

    [Fact]
    public void TheModuleApiExposesExactlyTheFiveMethodsOtherModulesDependOn() =>
        Assert.Equal(
            ["FindContactByEmailAsync", "FindPortalContactsByEmailAsync", "GetCustomerNamesAsync", "GetCustomerSummaryAsync", "GetPortalContactAsync"],
            typeof(ICustomersModuleApi).GetMethods().Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));

    [Fact]
    public void TheModuleApiMethodsAllTakeACancellationTokenLast() =>
        Assert.All(
            typeof(ICustomersModuleApi).GetMethods(),
            m => Assert.Equal(typeof(CancellationToken), m.GetParameters()[^1].ParameterType));
}

public sealed class CustomersDevSeedTests
{
    private static readonly FieldInfo[] GuidFields =
    [
        .. typeof(CustomersDevSeed).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(Guid)),
    ];

    private static Guid Value(FieldInfo field) => (Guid)(field.GetValue(null) ?? Guid.Empty);

    [Fact]
    public void EveryIdIsNonEmptyAndUnique()
    {
        Assert.Equal(16, GuidFields.Length);
        Assert.All(GuidFields, f => Assert.NotEqual(Guid.Empty, Value(f)));
        Assert.Equal(GuidFields.Length, GuidFields.Select(Value).Distinct().Count());
    }

    [Fact]
    public void CustomerContactAndNoteIdsFollowTheirPatterns()
    {
        foreach (FieldInfo field in GuidFields)
        {
            string text = Value(field).ToString();

            if (field.Name.EndsWith("CustomerId", StringComparison.Ordinal))
            {
                Assert.Contains("-0000-", text, StringComparison.Ordinal);
            }
            else if (field.Name.EndsWith("ContactId", StringComparison.Ordinal))
            {
                Assert.Contains("-0001-", text, StringComparison.Ordinal);
            }
            else if (field.Name.EndsWith("NoteId", StringComparison.Ordinal))
            {
                Assert.Contains("-0002-", text, StringComparison.Ordinal);
            }
            else
            {
                Assert.Fail($"Unexpected id field {field.Name}");
            }
        }
    }

    [Theory]
    [InlineData(CustomersDevSeed.AhmedEmail)]
    [InlineData(CustomersDevSeed.AlNoorMonaEmail)]
    [InlineData(CustomersDevSeed.AlNoorYousefEmail)]
    [InlineData(CustomersDevSeed.AlNoorHudaEmail)]
    [InlineData(CustomersDevSeed.FaisalEmail)]
    public void EveryEmailConstantIsAValidEmailAddress(string email) =>
        Assert.True(EmailAddress.TryCreate(email, out EmailAddress? parsed) && parsed.Value == email);

    [Fact]
    public void ThePhoneConstantIsAValidPhoneNumber()
    {
        Assert.True(PhoneNumber.TryCreate(CustomersDevSeed.AhmedPhone, out PhoneNumber? phone));
        Assert.Equal(CustomersDevSeed.AhmedPhone, phone.Value);
    }
}
