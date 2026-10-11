using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Customers.Tests.Domain;

/// <summary>Rules of a single note (the constructor is internal, so every case goes through the aggregate).</summary>
public sealed class CustomerNoteTests
{
    [Fact]
    public void TheBodyIsTrimmed() =>
        Assert.Equal("hi", CustomerFixtures.Individual().AddNote("  hi  ", Ids.Author, Clock.Now).Body);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankBodyIsRefused(string body) =>
        Expect.DomainCode(() => CustomerFixtures.Individual().AddNote(body, Ids.Author, Clock.Now), "Customers.NoteBodyRequired");

    [Fact]
    public void ANullBodyIsAnArgumentError()
    {
        Func<string, Guid, DateTimeOffset, Guid?, CustomerNote> add = CustomerFixtures.Individual().AddNote;

        NullArguments.AssertThrowsArgumentNull(add, null, Ids.Author, Clock.Now, null);
    }

    [Fact]
    public void TheBodyLimitIsFourThousandCharactersAfterTheTrim()
    {
        Customer customer = CustomerFixtures.Individual();

        Assert.Equal(4000, customer.AddNote(new string('b', 4000), Ids.Author, Clock.Now).Body.Length);
        Assert.Equal(4000, customer.AddNote(" " + new string('b', 4000) + " ", Ids.Author, Clock.Now).Body.Length);
        Expect.DomainCode(() => customer.AddNote(new string('b', 4001), Ids.Author, Clock.Now), "Customers.NoteBodyTooLong");
        Assert.Equal(4000, CustomerNote.BodyMaxLength);
    }

    [Fact]
    public void AnEmptyAuthorIsRefusedAfterTheBodyCheck()
    {
        Customer customer = CustomerFixtures.Individual();

        Expect.DomainCode(() => customer.AddNote("hi", Guid.Empty, Clock.Now), "Customers.NoteAuthorRequired");
        Expect.DomainCode(() => customer.AddNote("", Guid.Empty, Clock.Now), "Customers.NoteBodyRequired");
    }

    [Fact]
    public void ANewNoteKeepsItsAuthorAndTimestampAndIsNotPinned()
    {
        CustomerNote note = CustomerFixtures.Individual().AddNote("hi", Ids.Author, Clock.Later(5));

        Assert.Equal(Ids.Author, note.AuthorId);
        Assert.Equal(Clock.Later(5), note.CreatedAtUtc);
        Assert.False(note.IsPinned);
        Assert.Null(note.PinnedAtUtc);
        Assert.NotEqual(Guid.Empty, note.Id);
    }

    [Fact]
    public void ASuppliedIdWins() =>
        Assert.Equal(Ids.Note1, CustomerFixtures.Individual().AddNote("hi", Ids.Author, Clock.Now, Ids.Note1).Id);

    [Fact]
    public void ArabicTextRoundTrips() =>
        Assert.Equal("ملاحظة", CustomerFixtures.Individual().AddNote("ملاحظة", Ids.Author, Clock.Now).Body);

    [Fact]
    public void PinningSetsTheFlagAndTheTimestamp()
    {
        Customer customer = CustomerFixtures.Individual();
        CustomerNote note = customer.AddNote("hi", Ids.Author, Clock.Now, Ids.Note1);

        customer.PinNote(Ids.Note1, Clock.Later(1));

        Assert.True(note.IsPinned);
        Assert.Equal(Clock.Later(1), note.PinnedAtUtc);
    }

    [Fact]
    public void PinningIsIdempotentAndKeepsTheFirstTimestamp()
    {
        Customer customer = CustomerFixtures.Individual();
        CustomerNote note = customer.AddNote("hi", Ids.Author, Clock.Now, Ids.Note1);
        customer.PinNote(Ids.Note1, Clock.Later(1));

        customer.PinNote(Ids.Note1, Clock.Later(9));

        Assert.Equal(Clock.Later(1), note.PinnedAtUtc);
    }

    [Fact]
    public void UnpinningClearsTheFlagAndTheTimestampAndIsANoOpWhenNotPinned()
    {
        Customer customer = CustomerFixtures.Individual();
        CustomerNote note = customer.AddNote("hi", Ids.Author, Clock.Now, Ids.Note1);

        customer.UnpinNote(Ids.Note1);
        Assert.False(note.IsPinned);

        customer.PinNote(Ids.Note1, Clock.Later(1));
        customer.UnpinNote(Ids.Note1);

        Assert.False(note.IsPinned);
        Assert.Null(note.PinnedAtUtc);
    }

    [Fact]
    public void ARePinAfterAnUnpinTakesTheNewTimestamp()
    {
        Customer customer = CustomerFixtures.Individual();
        CustomerNote note = customer.AddNote("hi", Ids.Author, Clock.Now, Ids.Note1);
        customer.PinNote(Ids.Note1, Clock.Later(1));
        customer.UnpinNote(Ids.Note1);

        customer.PinNote(Ids.Note1, Clock.Later(7));

        Assert.Equal(Clock.Later(7), note.PinnedAtUtc);
    }
}

public sealed class CustomerNotesTests
{
    [Fact]
    public void AddingANoteAppendsItAndReturnsIt()
    {
        Customer customer = CustomerFixtures.Individual();

        CustomerNote note = customer.AddNote("hi", Ids.Author, Clock.Now);

        Assert.Same(note, Assert.Single(customer.Notes));
    }

    [Fact]
    public void NotesDoNotBumpTheRevision()
    {
        Customer customer = CustomerFixtures.Individual();

        customer.AddNote("a", Ids.Author, Clock.Now, Ids.Note1);
        customer.PinNote(Ids.Note1, Clock.Now);
        customer.UnpinNote(Ids.Note1);
        customer.RemoveNote(Ids.Note1);

        Assert.Equal(0, customer.Revision);
        Assert.Empty(customer.DomainEvents);
    }

    [Fact]
    public void AnUnknownNoteIsRefusedByEveryNoteMethod()
    {
        Customer customer = CustomerFixtures.Individual();

        Expect.DomainCode(() => customer.PinNote(Ids.Missing, Clock.Now), "Customers.NoteNotFound");
        Expect.DomainCode(() => customer.UnpinNote(Ids.Missing), "Customers.NoteNotFound");
        Expect.DomainCode(() => customer.RemoveNote(Ids.Missing), "Customers.NoteNotFound");
    }

    [Fact]
    public void RemovingANoteRemovesOnlyThatNote()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddNote("a", Ids.Author, Clock.Now, Ids.Note1);
        customer.AddNote("b", Ids.Author, Clock.Now, Ids.Note2);

        customer.RemoveNote(Ids.Note1);

        Assert.Equal(Ids.Note2, Assert.Single(customer.Notes).Id);
        Assert.False(customer.HasNote(Ids.Note1));
        Assert.True(customer.HasNote(Ids.Note2));
    }

    [Fact]
    public void NotesKeepTheirInsertionOrder()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddNote("a", Ids.Author, Clock.Later(3), Ids.Note1);
        customer.AddNote("b", Ids.Author, Clock.Later(1), Ids.Note2);
        customer.AddNote("c", Ids.Author, Clock.Later(2), Ids.Note3);

        Assert.Equal([Ids.Note1, Ids.Note2, Ids.Note3], customer.Notes.Select(n => n.Id));
    }

    [Fact]
    public void SeveralNotesCanBePinnedIndependently()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddNote("a", Ids.Author, Clock.Now, Ids.Note1);
        customer.AddNote("b", Ids.Author, Clock.Now, Ids.Note2);

        customer.PinNote(Ids.Note1, Clock.Later(1));
        customer.PinNote(Ids.Note2, Clock.Later(2));
        customer.UnpinNote(Ids.Note1);

        Assert.False(customer.Notes.Single(n => n.Id == Ids.Note1).IsPinned);
        Assert.True(customer.Notes.Single(n => n.Id == Ids.Note2).IsPinned);
    }

    [Fact]
    public void TheCollectionsAreReadOnlyViews()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddNote("a", Ids.Author, Clock.Now);
        customer.WithContacts(1);

        Assert.False(customer.Notes is IList<CustomerNote> { IsReadOnly: false });
        Assert.False(customer.Contacts is IList<Contact> { IsReadOnly: false });
        Assert.Single(customer.Notes);
        Assert.Single(customer.Contacts);
    }
}

public sealed class CustomerRevisionTests
{
    private static Customer Prepared()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);
        return customer;
    }

    public static TheoryData<string> Mutations => ["UpdateProfile", "Deactivate", "Reactivate", "AddContact", "UpdateContact", "SetPrimaryContact", "RemoveContact"];

    private static void Run(Customer customer, string mutation)
    {
        switch (mutation)
        {
            case "UpdateProfile":
                customer.UpdateProfile(CustomerFixtures.Name("a", "b"), CustomerType.Individual, null, PreferredLanguage.English);
                break;
            case "Deactivate":
                customer.Deactivate();
                break;
            case "Reactivate":
                customer.Deactivate();
                customer.Reactivate();
                break;
            case "AddContact":
                customer.AddSimpleContact("C", "c@example.com");
                break;
            case "UpdateContact":
                customer.UpdateContact(Ids.Contact2, "B2", null, CustomerFixtures.Email("b@example.com"), null, false);
                break;
            case "SetPrimaryContact":
                customer.SetPrimaryContact(Ids.Contact2);
                break;
            case "RemoveContact":
                customer.RemoveContact(Ids.Contact2);
                break;
            default:
                throw new InvalidOperationException(mutation);
        }
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public void EverySuccessfulMutationRaisesTheRevision(string mutation)
    {
        Customer customer = Prepared();
        int before = customer.Revision;

        Run(customer, mutation);

        Assert.True(customer.Revision > before);
    }

    [Fact]
    public void EveryFailedCallLeavesTheRevisionAlone()
    {
        Customer customer = Prepared();
        int before = customer.Revision;

        Expect.DomainCode(() => customer.Reactivate(), "Customers.AlreadyActive");
        Expect.DomainCode(() => customer.UpdateProfile(CustomerFixtures.Name(), CustomerType.Organisation, null, PreferredLanguage.English), "Customers.LegalNameRequired");
        Expect.DomainCode(() => customer.AddSimpleContact("C", "a@example.com"), "Customers.DuplicateContactEmail");
        Expect.DomainCode(() => customer.UpdateContact(Ids.Missing, "x", null, CustomerFixtures.Email("x@example.com"), null, false), "Customers.ContactNotFound");
        Expect.DomainCode(() => customer.SetPrimaryContact(Ids.Missing), "Customers.ContactNotFound");
        Expect.DomainCode(() => customer.RemoveContact(Ids.Contact1), "Customers.PrimaryContactRemoval");
        customer.Deactivate();
        Expect.DomainCode(() => customer.Deactivate(), "Customers.AlreadyInactive");

        Assert.Equal(before + 1, customer.Revision);
    }
}

/// <summary>The 25 codes of plan 11 are the frontend's errors.code.* contract. CustomerDomainErrors is internal, so
/// each code is provoked through the public aggregate and compared with the literal.</summary>
public sealed class DomainErrorCodesTests
{
    private static readonly string[] Contract =
    [
        "Customers.ScopeRequired", "Customers.NumberInvalid", "Customers.TypeUnsupported", "Customers.NameRequired",
        "Customers.NameTooLong", "Customers.LegalNameRequired", "Customers.LegalNameNotAllowed", "Customers.LegalNameTooLong",
        "Customers.LanguageUnsupported", "Customers.AlreadyActive", "Customers.AlreadyInactive", "Customers.EmailInvalid",
        "Customers.PhoneInvalid", "Customers.ContactNameRequired", "Customers.ContactNameTooLong", "Customers.JobTitleTooLong",
        "Customers.ContactChannelRequired", "Customers.PortalAccessRequiresEmail", "Customers.DuplicateContactEmail",
        "Customers.DuplicateContactPhone", "Customers.PrimaryContactRemoval", "Customers.ContactNotFound",
        "Customers.NoteNotFound", "Customers.NoteBodyRequired", "Customers.NoteBodyTooLong", "Customers.NoteAuthorRequired",
    ];

    private static Customer WithOneContact()
    {
        Customer customer = CustomerFixtures.Individual();
        customer.AddSimpleContact("A", "a@example.com", "+966500000001", id: Ids.Contact1);
        customer.AddSimpleContact("B", "b@example.com", id: Ids.Contact2);
        return customer;
    }

    private static readonly Dictionary<string, Action> Provokers = new()
    {
        ["Customers.ScopeRequired"] = () => Customer.Create(
            CustomerFixtures.Number(), CustomerFixtures.Name(), CustomerType.Individual, null, PreferredLanguage.English,
            TenantId.None, Ids.BranchA1, Ids.DepartmentA1),
        ["Customers.NumberInvalid"] = () => CustomerNumber.FromSequence(0),
        ["Customers.TypeUnsupported"] = () => CustomerFixtures.Individual().UpdateProfile(CustomerFixtures.Name(), (CustomerType)9, null, PreferredLanguage.English),
        ["Customers.NameRequired"] = () => LocalizedText.Create(null, null),
        ["Customers.NameTooLong"] = () => LocalizedText.Create(new string('a', 201), null),
        ["Customers.LegalNameRequired"] = () => CustomerFixtures.Organisation(legalName: null),
        ["Customers.LegalNameNotAllowed"] = () => Customer.Create(CustomerFixtures.Number(), CustomerFixtures.Name(), CustomerType.Individual, "Legal", PreferredLanguage.English, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1),
        ["Customers.LegalNameTooLong"] = () => CustomerFixtures.Organisation(legalName: new string('a', 301)),
        ["Customers.LanguageUnsupported"] = () => PreferredLanguage.Create("fr"),
        ["Customers.AlreadyActive"] = () => CustomerFixtures.Individual().Reactivate(),
        ["Customers.AlreadyInactive"] = () =>
        {
            Customer customer = CustomerFixtures.Individual();
            customer.Deactivate();
            customer.Deactivate();
        },
        ["Customers.EmailInvalid"] = () => EmailAddress.Create("nope"),
        ["Customers.PhoneInvalid"] = () => PhoneNumber.Create("nope"),
        ["Customers.ContactNameRequired"] = () => CustomerFixtures.Individual().AddSimpleContact(" ", "a@example.com"),
        ["Customers.ContactNameTooLong"] = () => CustomerFixtures.Individual().AddSimpleContact(new string('a', 201), "a@example.com"),
        ["Customers.JobTitleTooLong"] = () => CustomerFixtures.Individual().AddContact(
            "A", new string('a', 101), CustomerFixtures.Email("a@example.com"), null, false, false),
        ["Customers.ContactChannelRequired"] = () => CustomerFixtures.Individual().AddContact("A", null, null, null, false, false),
        ["Customers.PortalAccessRequiresEmail"] = () => CustomerFixtures.Individual().AddSimpleContact("A", null, "+966500000001", portal: true),
        ["Customers.DuplicateContactEmail"] = () => WithOneContact().AddSimpleContact("C", "a@example.com"),
        ["Customers.DuplicateContactPhone"] = () => WithOneContact().AddSimpleContact("C", null, "+966500000001"),
        ["Customers.PrimaryContactRemoval"] = () => WithOneContact().RemoveContact(Ids.Contact1),
        ["Customers.ContactNotFound"] = () => WithOneContact().SetPrimaryContact(Ids.Missing),
        ["Customers.NoteNotFound"] = () => WithOneContact().RemoveNote(Ids.Missing),
        ["Customers.NoteBodyRequired"] = () => WithOneContact().AddNote(" ", Ids.Author, Clock.Now),
        ["Customers.NoteBodyTooLong"] = () => WithOneContact().AddNote(new string('a', 4001), Ids.Author, Clock.Now),
        ["Customers.NoteAuthorRequired"] = () => WithOneContact().AddNote("a", Guid.Empty, Clock.Now),
    };

    [Fact]
    public void TheProvokersCoverExactlyTheTwentySixCodesOfTheContract() =>
        Assert.Equal(Contract.OrderBy(c => c, StringComparer.Ordinal), Provokers.Keys.OrderBy(c => c, StringComparer.Ordinal));

    [Fact]
    public void EveryCodeIsRaisedExactlyAsNamed()
    {
        foreach ((string code, Action provoke) in Provokers)
        {
            Expect.DomainCode(provoke, code);
        }
    }
}
