using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Customers.Tests.Domain;

public sealed class CustomerCreateTests
{
    private static Customer Create(
        CustomerType type = CustomerType.Individual, string? legalName = null, TenantId? tenant = null, CustomerId? id = null) =>
        Customer.Create(
            CustomerFixtures.Number(), CustomerFixtures.Name(), type, legalName, PreferredLanguage.English,
            tenant ?? Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, id);

    [Fact]
    public void ANewCustomerCarriesEverythingItWasGivenAndStartsActive()
    {
        Customer customer = Customer.Create(
            CustomerFixtures.Number(5), CustomerFixtures.Name("نجد", "Najd"), CustomerType.Organisation, "  Najd Co  ",
            PreferredLanguage.Arabic, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA2);

        Assert.Equal("C-000005", customer.Number.Value);
        Assert.Equal("Najd", customer.DisplayName.English);
        Assert.Equal(CustomerType.Organisation, customer.Type);
        Assert.Equal("Najd Co", customer.LegalName);
        Assert.Same(PreferredLanguage.Arabic, customer.PreferredLanguage);
        Assert.Equal(Ids.TenantA, customer.TenantId);
        Assert.Equal(Ids.BranchA1, customer.BranchId);
        Assert.Equal(Ids.DepartmentA2, customer.DepartmentId);
        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.True(customer.IsActive);
        Assert.Equal(0, customer.Revision);
        Assert.Empty(customer.Contacts);
        Assert.Empty(customer.Notes);
        Assert.False(customer.IsDeleted);
        Assert.Empty(customer.RowVersion);
        Assert.NotEqual(Guid.Empty, customer.Id.Value);
        Assert.Empty(customer.DomainEvents);
    }

    [Fact]
    public void ASuppliedIdWins() =>
        Assert.Equal(Ids.Customer1, Create(id: Ids.Customer1).Id);

    [Fact]
    public void WithoutAnIdAFreshOneIsGenerated() =>
        Assert.NotEqual(Create().Id, Create().Id);

    [Fact]
    public void ANoneTenantIsRefused() =>
        Expect.DomainCode(() => Create(tenant: TenantId.None), "Customers.ScopeRequired");

    [Fact]
    public void EveryRequiredArgumentIsGuarded()
    {
        Func<CustomerNumber, LocalizedText, CustomerType, string?, PreferredLanguage, TenantId, BranchId, DepartmentId, CustomerId?, Customer> create =
            Customer.Create;

        CustomerNumber number = CustomerFixtures.Number();
        LocalizedText name = CustomerFixtures.Name();
        PreferredLanguage language = PreferredLanguage.English;

        NullArguments.AssertThrowsArgumentNull(create, null, name, CustomerType.Individual, null, language, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, null);
        NullArguments.AssertThrowsArgumentNull(create, number, null, CustomerType.Individual, null, language, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, null);
        NullArguments.AssertThrowsArgumentNull(create, number, name, CustomerType.Individual, null, null, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, null);
        NullArguments.AssertThrowsArgumentNull(create, number, name, CustomerType.Individual, null, language, null, Ids.BranchA1, Ids.DepartmentA1, null);
        NullArguments.AssertThrowsArgumentNull(create, number, name, CustomerType.Individual, null, language, Ids.TenantA, null, Ids.DepartmentA1, null);
        NullArguments.AssertThrowsArgumentNull(create, number, name, CustomerType.Individual, null, language, Ids.TenantA, Ids.BranchA1, null, null);
    }

    [Fact]
    public void AnUndefinedTypeIsRefused() =>
        Expect.DomainCode(() => Create((CustomerType)7), "Customers.TypeUnsupported");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnOrganisationRequiresALegalName(string? legalName) =>
        Expect.DomainCode(() => Create(CustomerType.Organisation, legalName), "Customers.LegalNameRequired");

    [Fact]
    public void AnOrganisationLegalNameIsTrimmedAndLimitedToThreeHundredCharacters()
    {
        Assert.Equal("Najd Co", Create(CustomerType.Organisation, "  Najd Co ").LegalName);
        Assert.Equal(300, Create(CustomerType.Organisation, new string('n', 300)).LegalName?.Length);
        Expect.DomainCode(() => Create(CustomerType.Organisation, new string('n', 301)), "Customers.LegalNameTooLong");
        Assert.Equal(300, Customer.LegalNameMaxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnIndividualMayOmitTheLegalName(string? legalName) =>
        Assert.Null(Create(CustomerType.Individual, legalName).LegalName);

    [Fact]
    public void AnIndividualCannotHaveALegalName() =>
        Expect.DomainCode(() => Create(CustomerType.Individual, "Najd Co"), "Customers.LegalNameNotAllowed");

    [Fact]
    public void TheScopeIsCheckedBeforeTheTypeAndTheTypeBeforeTheLegalName()
    {
        Expect.DomainCode(() => Create((CustomerType)7, null, TenantId.None), "Customers.ScopeRequired");
        Expect.DomainCode(() => Create((CustomerType)7, "Najd Co"), "Customers.TypeUnsupported");
    }
}

public sealed class CustomerUpdateProfileTests
{
    [Fact]
    public void TheProfileIsReplacedAndTheRevisionGoesUp()
    {
        Customer customer = CustomerFixtures.Individual();

        customer.UpdateProfile(CustomerFixtures.Name("سارة", "Sara"), CustomerType.Individual, null, PreferredLanguage.Arabic);

        Assert.Equal("Sara", customer.DisplayName.English);
        Assert.Same(PreferredLanguage.Arabic, customer.PreferredLanguage);
        Assert.Equal(1, customer.Revision);
        Assert.Empty(customer.DomainEvents);
    }

    [Fact]
    public void SwitchingToAnOrganisationNeedsALegalName()
    {
        Customer customer = CustomerFixtures.Individual();

        Expect.DomainCode(
            () => customer.UpdateProfile(CustomerFixtures.Name(), CustomerType.Organisation, null, PreferredLanguage.English),
            "Customers.LegalNameRequired");
        customer.UpdateProfile(CustomerFixtures.Name(), CustomerType.Organisation, " Najd Co ", PreferredLanguage.English);

        Assert.Equal(CustomerType.Organisation, customer.Type);
        Assert.Equal("Najd Co", customer.LegalName);
    }

    [Fact]
    public void SwitchingToAnIndividualClearsTheLegalNameButCannotKeepIt()
    {
        Customer customer = CustomerFixtures.Organisation();

        Expect.DomainCode(
            () => customer.UpdateProfile(CustomerFixtures.Name(), CustomerType.Individual, "Najd Co", PreferredLanguage.English),
            "Customers.LegalNameNotAllowed");
        customer.UpdateProfile(CustomerFixtures.Name(), CustomerType.Individual, null, PreferredLanguage.English);

        Assert.Equal(CustomerType.Individual, customer.Type);
        Assert.Null(customer.LegalName);
    }

    [Fact]
    public void ATooLongLegalNameIsRefused() =>
        Expect.DomainCode(
            () => CustomerFixtures.Organisation().UpdateProfile(
                CustomerFixtures.Name(), CustomerType.Organisation, new string('n', 301), PreferredLanguage.English),
            "Customers.LegalNameTooLong");

    [Fact]
    public void ARefusedUpdateChangesNothingAndLeavesTheRevisionAlone()
    {
        Customer customer = CustomerFixtures.Organisation();

        Expect.DomainCode(
            () => customer.UpdateProfile(CustomerFixtures.Name("x", "y"), (CustomerType)7, "Other", PreferredLanguage.Arabic),
            "Customers.TypeUnsupported");
        Expect.DomainCode(
            () => customer.UpdateProfile(CustomerFixtures.Name("x", "y"), CustomerType.Organisation, null, PreferredLanguage.Arabic),
            "Customers.LegalNameRequired");

        Assert.Equal("Najd", customer.DisplayName.English);
        Assert.Equal("Najd Co", customer.LegalName);
        Assert.Same(PreferredLanguage.English, customer.PreferredLanguage);
        Assert.Equal(0, customer.Revision);
    }

    [Fact]
    public void EveryArgumentIsGuarded()
    {
        Customer customer = CustomerFixtures.Individual();
        Action<LocalizedText, CustomerType, string?, PreferredLanguage> update = customer.UpdateProfile;

        NullArguments.AssertThrowsArgumentNull(update, null, CustomerType.Individual, null, PreferredLanguage.English);
        NullArguments.AssertThrowsArgumentNull(update, CustomerFixtures.Name(), CustomerType.Individual, null, null);
    }

    [Fact]
    public void TheIdentityAndScopeNeverChange()
    {
        Customer customer = CustomerFixtures.Individual(sequence: 9, id: Ids.Customer1);

        customer.UpdateProfile(CustomerFixtures.Name("a", "b"), CustomerType.Individual, null, PreferredLanguage.Arabic);

        Assert.Equal("C-000009", customer.Number.Value);
        Assert.Equal(Ids.Customer1, customer.Id);
        Assert.Equal(Ids.TenantA, customer.TenantId);
        Assert.Equal(Ids.BranchA1, customer.BranchId);
        Assert.Equal(Ids.DepartmentA1, customer.DepartmentId);
        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public void AnInactiveCustomerStaysEditableAndStaysInactive()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.Deactivate();

        customer.UpdateProfile(CustomerFixtures.Name("a", "b"), CustomerType.Individual, null, PreferredLanguage.English);

        Assert.Equal("b", customer.DisplayName.English);
        Assert.Equal(CustomerStatus.Inactive, customer.Status);
    }
}

public sealed class CustomerStatusTests
{
    [Fact]
    public void DeactivatingAnActiveCustomerMakesItInactive()
    {
        Customer customer = CustomerFixtures.Individual();

        customer.Deactivate();

        Assert.Equal(CustomerStatus.Inactive, customer.Status);
        Assert.False(customer.IsActive);
        Assert.Equal(1, customer.Revision);
        Assert.Empty(customer.DomainEvents);
    }

    [Fact]
    public void DeactivatingTwiceIsRefusedAndKeepsTheRevision()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.Deactivate();

        Expect.DomainCode(customer.Deactivate, "Customers.AlreadyInactive");

        Assert.Equal(1, customer.Revision);
    }

    [Fact]
    public void ReactivatingAnInactiveCustomerMakesItActive()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.Deactivate();

        customer.Reactivate();

        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.True(customer.IsActive);
        Assert.Equal(2, customer.Revision);
    }

    [Fact]
    public void ReactivatingAnActiveCustomerIsRefused()
    {
        Customer customer = CustomerFixtures.Individual();

        Expect.DomainCode(customer.Reactivate, "Customers.AlreadyActive");

        Assert.Equal(0, customer.Revision);
    }

    [Fact]
    public void ACycleOfDeactivateAndReactivateWorksAndLeavesContactsAndNotesAlone()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.WithContacts(2);
        customer.AddNote("note", Ids.Author, Clock.Now);

        customer.Deactivate();
        customer.Reactivate();
        customer.Deactivate();

        Assert.Equal(CustomerStatus.Inactive, customer.Status);
        Assert.Equal(2, customer.Contacts.Count);
        Assert.Single(customer.Notes);
        Assert.Equal(5, customer.Revision);
    }
}
