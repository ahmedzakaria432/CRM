using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;

namespace Crm.Modules.Customers.Tests.Domain;

/// <summary>Contact rules of a single contact (the class is internal-constructed, so every case goes through the aggregate).</summary>
public sealed class ContactTests
{
    private static Contact Add(
        Customer customer, string name = "Sara", string? title = null, string? email = "sara@example.com", string? phone = null,
        bool portal = false) =>
        customer.AddContact(
            name, title, email is null ? null : EmailAddress.Create(email), phone is null ? null : PhoneNumber.Create(phone), false, portal);

    [Fact]
    public void TheNameIsTrimmed() =>
        Assert.Equal("Sara", Add(CustomerFixtures.Individual(), "  Sara  ").Name);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsRefused(string name) =>
        Expect.DomainCode(() => Add(CustomerFixtures.Individual(), name), "Customers.ContactNameRequired");

    [Fact]
    public void ANullNameIsAnArgumentError()
    {
        Customer customer = CustomerFixtures.Individual();
        Func<string, string?, EmailAddress?, PhoneNumber?, bool, bool, Guid?, Contact> add = customer.AddContact;

        NullArguments.AssertThrowsArgumentNull(add, null, null, CustomerFixtures.Email("a@b.co"), null, false, false, null);
    }

    [Fact]
    public void TheNameLimitIsTwoHundredCharacters()
    {
        Customer customer = CustomerFixtures.Individual();

        Assert.Equal(200, Add(customer, new string('n', 200)).Name.Length);
        Expect.DomainCode(() => Add(customer, new string('n', 201), email: "other@example.com"), "Customers.ContactNameTooLong");
        Assert.Equal(200, Contact.NameMaxLength);
    }

    [Fact]
    public void TheJobTitleIsTrimmedAndABlankOneBecomesNull()
    {
        Customer customer = CustomerFixtures.Individual();

        Assert.Equal("Manager", Add(customer, title: "  Manager ").JobTitle);
        Assert.Null(Add(customer, title: "   ", email: "b@example.com").JobTitle);
        Assert.Null(Add(customer, title: null, email: "c@example.com").JobTitle);
    }

    [Fact]
    public void TheJobTitleLimitIsOneHundredCharacters()
    {
        Customer customer = CustomerFixtures.Individual();

        Assert.Equal(100, Add(customer, title: new string('t', 100)).JobTitle?.Length);
        Expect.DomainCode(() => Add(customer, title: new string('t', 101), email: "x@example.com"), "Customers.JobTitleTooLong");
        Assert.Equal(100, Contact.JobTitleMaxLength);
    }

    [Theory]
    [InlineData("a@example.com", null)]
    [InlineData(null, "+966500000001")]
    [InlineData("a@example.com", "+966500000001")]
    public void EitherChannelOrBothIsEnough(string? email, string? phone)
    {
        Contact contact = Add(CustomerFixtures.Individual(), email: email, phone: phone);

        Assert.Equal(email, contact.Email?.Value);
        Assert.Equal(phone, contact.Phone?.Value);
    }

    [Fact]
    public void ANeitherChannelContactIsRefused() =>
        Expect.DomainCode(() => Add(CustomerFixtures.Individual(), email: null, phone: null), "Customers.ContactChannelRequired");

    [Fact]
    public void PortalAccessNeedsAnEmail()
    {
        Customer customer = CustomerFixtures.Individual();

        Assert.True(Add(customer, email: "a@example.com", portal: true).HasPortalAccess);
        Expect.DomainCode(
            () => Add(customer, email: null, phone: "+966500000001", portal: true), "Customers.PortalAccessRequiresEmail");
        Assert.False(Add(customer, email: null, phone: "+966500000001", portal: false).HasPortalAccess);
    }

    [Fact]
    public void TheRulesAreCheckedInTheOrderNameThenChannelThenPortalAccess()
    {
        Customer customer = CustomerFixtures.Individual();

        Expect.DomainCode(() => Add(customer, "", email: null, phone: null, portal: true), "Customers.ContactNameRequired");
        Expect.DomainCode(() => Add(customer, "Sara", email: null, phone: null, portal: true), "Customers.ContactChannelRequired");
    }

    [Fact]
    public void AFailedUpdateLeavesTheOldValuesIntact()
    {
        Customer customer = CustomerFixtures.Individual();
        Contact contact = customer.AddSimpleContact("Sara", "a@example.com", "+966500000001", portal: true, id: Ids.Contact1);

        Expect.DomainCode(
            () => customer.UpdateContact(Ids.Contact1, "Renamed", null, null, PhoneNumber.Create("+966500000009"), true),
            "Customers.PortalAccessRequiresEmail");

        Assert.Equal("Sara", contact.Name);
        Assert.Equal("a@example.com", contact.Email?.Value);
        Assert.Equal("+966500000001", contact.Phone?.Value);
        Assert.True(contact.HasPortalAccess);
    }

    [Fact]
    public void AnUpdateKeepsTheIdAndThePrimaryFlag()
    {
        Customer customer = CustomerFixtures.Individual();
        Contact contact = customer.AddSimpleContact("Sara", "a@example.com", id: Ids.Contact1);

        customer.UpdateContact(Ids.Contact1, "Sara B", "Boss", CustomerFixtures.Email("b@example.com"), null, false);

        Assert.Equal(Ids.Contact1, contact.Id);
        Assert.True(contact.IsPrimary);
        Assert.Equal("Sara B", contact.Name);
        Assert.Equal("Boss", contact.JobTitle);
    }

    [Fact]
    public void ASuppliedIdWinsAndOtherwiseOneIsGenerated()
    {
        Customer customer = CustomerFixtures.Individual();

        Assert.Equal(Ids.Contact1, customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1).Id);
        Assert.NotEqual(Guid.Empty, customer.AddSimpleContact("B", "b@example.com").Id);
    }
}

public sealed class CustomerAddContactTests
{
    [Fact]
    public void TheFirstContactBecomesPrimaryEvenWhenNotAskedTo()
    {
        Customer customer = CustomerFixtures.Individual();

        Contact first = customer.AddSimpleContact("A", "a@example.com", primary: false);

        Assert.True(first.IsPrimary);
    }

    [Fact]
    public void ASecondNonPrimaryContactLeavesTheFirstPrimary()
    {
        Customer customer = CustomerFixtures.Individual();
        Contact first = customer.AddSimpleContact("A", "a@example.com");

        Contact second = customer.AddSimpleContact("B", "b@example.com", primary: false);

        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
    }

    [Fact]
    public void ASecondPrimaryContactDemotesTheFirst()
    {
        Customer customer = CustomerFixtures.Individual();
        Contact first = customer.AddSimpleContact("A", "a@example.com");

        Contact second = customer.AddSimpleContact("B", "b@example.com", primary: true);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Single(customer.Contacts, c => c.IsPrimary);
    }

    [Fact]
    public void TheRevisionGoesUpByOnePerCall()
    {
        Customer customer = CustomerFixtures.Individual();

        customer.WithContacts(3);

        Assert.Equal(3, customer.Revision);
    }

    [Fact]
    public void AnEmailThatDiffersOnlyByCaseOrFormatIsADuplicate()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@x.example");

        Expect.DomainCode(() => customer.AddSimpleContact("B", "A@X.example"), "Customers.DuplicateContactEmail");
        Expect.DomainCode(() => customer.AddSimpleContact("B", " a@x.EXAMPLE "), "Customers.DuplicateContactEmail");
    }

    [Fact]
    public void APhoneThatDiffersOnlyBySeparatorsIsADuplicate()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", null, "+966551112233");

        Expect.DomainCode(() => customer.AddSimpleContact("B", null, "+966 55 111 2233"), "Customers.DuplicateContactPhone");
    }

    [Fact]
    public void TheEmailIsCheckedBeforeThePhone()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", "+966551112233");

        Expect.DomainCode(() => customer.AddSimpleContact("B", "a@example.com", "+966551112233"), "Customers.DuplicateContactEmail");
    }

    [Fact]
    public void TheDuplicateCheckRunsBeforeTheContactIsBuilt()
    {
        // Pinned: a blank name with a duplicate email reports the duplicate, not the missing name.
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com");

        Expect.DomainCode(() => customer.AddSimpleContact("", "a@example.com"), "Customers.DuplicateContactEmail");
    }

    [Fact]
    public void ARejectedAddLeavesContactsPrimaryAndRevisionUnchanged()
    {
        Customer customer = CustomerFixtures.Individual();
        Contact first = customer.AddSimpleContact("A", "a@example.com");

        Expect.DomainCode(() => customer.AddSimpleContact("B", "a@example.com", primary: true), "Customers.DuplicateContactEmail");
        Expect.DomainCode(() => customer.AddSimpleContact("C", null, null, primary: true), "Customers.ContactChannelRequired");

        Assert.Single(customer.Contacts);
        Assert.True(first.IsPrimary);
        Assert.Equal(1, customer.Revision);
    }

    [Fact]
    public void ContactsWithoutAnEmailMayShareTheAbsenceButNotThePhone()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", null, "+966500000001");

        customer.AddSimpleContact("B", null, "+966500000002");

        Assert.Equal(2, customer.Contacts.Count);
        Expect.DomainCode(() => customer.AddSimpleContact("C", null, "+966500000001"), "Customers.DuplicateContactPhone");
    }

    [Fact]
    public void AnEmailOfARemovedContactCanBeUsedAgain()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);
        customer.RemoveContact(Ids.Contact2);

        Contact again = customer.AddSimpleContact("B2", "b@example.com");

        Assert.Equal("b@example.com", again.Email?.Value);
    }

    [Fact]
    public void TheAggregateDoesNotLimitTheNumberOfContacts()
    {
        Customer customer = CustomerFixtures.Individual();

        customer.WithContacts(25);

        Assert.Equal(25, customer.Contacts.Count);
    }

    [Fact]
    public void ASuppliedIdWins() =>
        Assert.Equal(Ids.Contact3, CustomerFixtures.Individual().AddSimpleContact("A", "a@example.com", id: Ids.Contact3).Id);
}

public sealed class CustomerUpdateContactTests
{
    [Fact]
    public void AnUnknownContactIsRefused() =>
        Expect.DomainCode(
            () => CustomerFixtures.Individual().UpdateContact(Ids.Missing, "A", null, CustomerFixtures.Email("a@example.com"), null, false),
            "Customers.ContactNotFound");

    [Fact]
    public void AnUpdateReplacesEveryFieldAndBumpsTheRevision()
    {
        Customer customer = CustomerFixtures.Individual();
        Contact contact = customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);

        customer.UpdateContact(
            Ids.Contact1, "  New Name ", " Boss ", CustomerFixtures.Email("new@example.com"), CustomerFixtures.Phone("+966500000007"), true);

        Assert.Equal("New Name", contact.Name);
        Assert.Equal("Boss", contact.JobTitle);
        Assert.Equal("new@example.com", contact.Email?.Value);
        Assert.Equal("+966500000007", contact.Phone?.Value);
        Assert.True(contact.HasPortalAccess);
        Assert.Equal(2, customer.Revision);
    }

    [Fact]
    public void KeepingItsOwnEmailAndPhoneIsNotADuplicate()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", "+966500000001", id: Ids.Contact1);

        customer.UpdateContact(
            Ids.Contact1, "A2", null, CustomerFixtures.Email("a@example.com"), CustomerFixtures.Phone("+966500000001"), false);

        Assert.Equal("A2", customer.Contacts.Single().Name);
    }

    [Fact]
    public void TakingAnotherContactsEmailOrPhoneIsADuplicate()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", "+966500000001", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", "+966500000002", id: Ids.Contact2);

        Expect.DomainCode(
            () => customer.UpdateContact(Ids.Contact2, "B", null, CustomerFixtures.Email("a@example.com"), null, false),
            "Customers.DuplicateContactEmail");
        Expect.DomainCode(
            () => customer.UpdateContact(Ids.Contact2, "B", null, CustomerFixtures.Email("b@example.com"), CustomerFixtures.Phone("+966500000001"), false),
            "Customers.DuplicateContactPhone");
    }

    [Theory]
    [InlineData("", "Customers.ContactNameRequired")]
    [InlineData("   ", "Customers.ContactNameRequired")]
    public void ContactLevelRulesSurface(string name, string code)
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);

        Expect.DomainCode(() => customer.UpdateContact(Ids.Contact1, name, null, CustomerFixtures.Email("a@example.com"), null, false), code);
    }

    [Fact]
    public void ANeitherChannelUpdateIsRefusedAndNothingChanges()
    {
        Customer customer = CustomerFixtures.Individual();
        Contact contact = customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);

        Expect.DomainCode(() => customer.UpdateContact(Ids.Contact1, "A", null, null, null, false), "Customers.ContactChannelRequired");

        Assert.Equal("a@example.com", contact.Email?.Value);
        Assert.Equal(1, customer.Revision);
    }

    [Fact]
    public void AFailedUpdateForADuplicateLeavesTheContactAndTheRevisionAlone()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);
        Contact second = customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);

        Expect.DomainCode(
            () => customer.UpdateContact(Ids.Contact2, "Renamed", null, CustomerFixtures.Email("a@example.com"), null, false),
            "Customers.DuplicateContactEmail");

        Assert.Equal("B", second.Name);
        Assert.Equal(2, customer.Revision);
    }

    [Fact]
    public void ClearingTheEmailIsAllowedWhileAPhoneRemainsAndPortalAccessIsOff()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", "+966500000001", id: Ids.Contact1);

        customer.UpdateContact(Ids.Contact1, "A", null, null, CustomerFixtures.Phone("+966500000001"), false);

        Assert.Null(customer.Contacts.Single().Email);
    }

    [Fact]
    public void ClearingTheEmailWhilePortalAccessStaysOnIsRefused()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", "+966500000001", portal: true, id: Ids.Contact1);

        Expect.DomainCode(
            () => customer.UpdateContact(Ids.Contact1, "A", null, null, CustomerFixtures.Phone("+966500000001"), true),
            "Customers.PortalAccessRequiresEmail");
    }

    [Fact]
    public void ThePrimaryFlagDoesNotChange()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);

        customer.UpdateContact(Ids.Contact2, "B2", null, CustomerFixtures.Email("b@example.com"), null, false);

        Assert.True(customer.Contacts.Single(c => c.Id == Ids.Contact1).IsPrimary);
        Assert.False(customer.Contacts.Single(c => c.Id == Ids.Contact2).IsPrimary);
    }
}

public sealed class CustomerSetPrimaryContactTests
{
    [Fact]
    public void ThePromotedContactIsTheOnlyPrimary()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);
        customer.AddSimpleContact("C", "c@example.com", id: Ids.Contact3);

        customer.SetPrimaryContact(Ids.Contact3);

        Assert.Equal(Ids.Contact3, Assert.Single(customer.Contacts, c => c.IsPrimary).Id);
    }

    [Fact]
    public void SettingTheCurrentPrimaryAgainIsAcceptedAndStillBumpsTheRevision()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);

        customer.SetPrimaryContact(Ids.Contact1);

        Assert.True(customer.Contacts.Single().IsPrimary);
        Assert.Equal(2, customer.Revision);
    }

    [Fact]
    public void AnUnknownContactIsRefusedAndNothingChanges()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);

        Expect.DomainCode(() => customer.SetPrimaryContact(Ids.Missing), "Customers.ContactNotFound");

        Assert.True(customer.Contacts.Single().IsPrimary);
        Assert.Equal(1, customer.Revision);
    }
}

public sealed class CustomerRemoveContactTests
{
    [Fact]
    public void ANonPrimaryContactCanBeRemoved()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);

        customer.RemoveContact(Ids.Contact2);

        Assert.Equal(Ids.Contact1, Assert.Single(customer.Contacts).Id);
        Assert.Equal(3, customer.Revision);
    }

    [Fact]
    public void ThePrimaryCannotBeRemovedWhileOthersExist()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);

        Expect.DomainCode(() => customer.RemoveContact(Ids.Contact1), "Customers.PrimaryContactRemoval");

        Assert.Equal(2, customer.Contacts.Count);
        Assert.Equal(2, customer.Revision);
    }

    [Fact]
    public void PromotingAnotherContactFirstMakesTheRemovalPossible()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);

        customer.SetPrimaryContact(Ids.Contact2);
        customer.RemoveContact(Ids.Contact1);

        Assert.Equal(Ids.Contact2, Assert.Single(customer.Contacts).Id);
    }

    [Fact]
    public void TheLastContactCanBeRemovedEvenThoughItIsPrimary()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);

        customer.RemoveContact(Ids.Contact1);

        Assert.Empty(customer.Contacts);
    }

    [Fact]
    public void AnUnknownContactIsRefused() =>
        Expect.DomainCode(() => CustomerFixtures.Individual().RemoveContact(Ids.Missing), "Customers.ContactNotFound");

    [Fact]
    public void HasContactFollowsTheContacts()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);

        Assert.True(customer.HasContact(Ids.Contact1));
        Assert.False(customer.HasContact(Ids.Contact2));

        customer.RemoveContact(Ids.Contact1);

        Assert.False(customer.HasContact(Ids.Contact1));
    }
}
