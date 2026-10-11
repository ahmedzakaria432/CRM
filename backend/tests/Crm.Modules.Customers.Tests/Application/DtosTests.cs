using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;

namespace Crm.Modules.Customers.Tests.Application;

public sealed class DtosTests
{
    private static Customer Rich()
    {
        Customer customer = CustomerFixtures.Organisation(9, "نجد", "Najd", "Najd Co", "ar", id: Ids.Customer1);
        customer.AddContact("b", "Boss", CustomerFixtures.Email("b@example.com"), null, true, false, Ids.Contact1);
        customer.AddContact("A", null, null, CustomerFixtures.Phone("+966500000001"), false, false, Ids.Contact2);
        customer.AddContact("a", null, CustomerFixtures.Email("a@example.com"), CustomerFixtures.Phone("+966500000002"), false, true, Ids.Contact3);
        customer.AddNote("first", Ids.Author, Clock.Later(1), Ids.Note1);
        customer.AddNote("second", Ids.Author, Clock.Later(2), Ids.Note2);
        customer.AddNote("third", Ids.Author, Clock.Later(2), Ids.Note3);
        customer.PinNote(Ids.Note1, Clock.Later(10));
        return customer;
    }

    [Fact]
    public void ANameMapsBothSides()
    {
        CustomerNameDto dto = CustomerNameDto.FromDomain(CustomerFixtures.Name("أ", "A"));

        Assert.Equal(new CustomerNameDto("أ", "A"), dto);
    }

    [Fact]
    public void AContactMapsItsValuesOrNulls()
    {
        Customer customer = Rich();

        ContactDto withBoth = ContactDto.FromDomain(customer.Contacts.Single(c => c.Id == Ids.Contact3));
        ContactDto phoneOnly = ContactDto.FromDomain(customer.Contacts.Single(c => c.Id == Ids.Contact2));
        ContactDto emailOnly = ContactDto.FromDomain(customer.Contacts.Single(c => c.Id == Ids.Contact1));

        Assert.Equal(new ContactDto(Ids.Contact3, "a", null, "a@example.com", "+966500000002", false, true), withBoth);
        Assert.Null(phoneOnly.Email);
        Assert.Equal("+966500000001", phoneOnly.Phone);
        Assert.Null(emailOnly.Phone);
        Assert.Equal("Boss", emailOnly.JobTitle);
        Assert.True(emailOnly.IsPrimary);
    }

    [Fact]
    public void ANoteMapsEveryField()
    {
        CustomerNote note = Rich().Notes.Single(n => n.Id == Ids.Note1);

        Assert.Equal(new CustomerNoteDto(Ids.Note1, "first", true, Ids.Author, Clock.Later(1), Clock.Later(10)), CustomerNoteDto.FromDomain(note));
    }

    [Fact]
    public void ACustomerOrdersContactsPrimaryFirstThenByOrdinalNameAndNotesPinnedFirstThenNewestFirst()
    {
        CustomerDto dto = CustomerDto.FromDomain(Rich());

        Assert.Equal(["b", "A", "a"], dto.Contacts.Select(c => c.Name));
        // second and third share a timestamp: the order between them is the sort's stable one (insertion order).
        Assert.Equal(Ids.Note1, dto.Notes[0].Id);
        Assert.Equal([Ids.Note2, Ids.Note3], dto.Notes.Skip(1).Select(n => n.Id));
    }

    [Fact]
    public void ACustomerMapsEveryField()
    {
        Customer customer = Rich();

        CustomerDto dto = CustomerDto.FromDomain(customer);

        Assert.Equal(Ids.Customer1.Value, dto.Id);
        Assert.Equal("C-000009", dto.Number);
        Assert.Equal(new CustomerNameDto("نجد", "Najd"), dto.DisplayName);
        Assert.Equal("Organisation", dto.Type);
        Assert.Equal("Najd Co", dto.LegalName);
        Assert.Equal("Active", dto.Status);
        Assert.Equal("ar", dto.PreferredLanguage);
        Assert.Equal(Ids.TenantA.Value, dto.TenantId);
        Assert.Equal(Ids.BranchA1.Value, dto.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, dto.DepartmentId);
        Assert.Same(customer.RowVersion, dto.RowVersion);
        Assert.Null(dto.CreatedBy);
        Assert.Null(dto.CreatedAtUtc);
        Assert.Null(dto.ModifiedBy);
        Assert.Null(dto.ModifiedAtUtc);
    }

    [Fact]
    public void AListItemUsesThePrimaryContactAndAnEmptyCustomerGivesNulls()
    {
        CustomerListItemDto rich = CustomerListItemDto.FromDomain(Rich());
        CustomerListItemDto empty = CustomerListItemDto.FromDomain(CustomerFixtures.Individual(id: Ids.Customer2));

        Assert.Equal("b", rich.PrimaryContactName);
        Assert.Equal("b@example.com", rich.PrimaryEmail);
        Assert.Null(rich.PrimaryPhone);
        Assert.Equal("Organisation", rich.Type);
        Assert.Equal("Active", rich.Status);
        Assert.Null(empty.PrimaryContactName);
        Assert.Null(empty.PrimaryEmail);
        Assert.Null(empty.PrimaryPhone);
    }

    [Fact]
    public void ALookupItemUsesThePrimaryContactAndAnEmptyCustomerGivesNulls()
    {
        CustomerLookupDto rich = CustomerLookupDto.FromDomain(Rich());
        CustomerLookupDto empty = CustomerLookupDto.FromDomain(CustomerFixtures.Individual(id: Ids.Customer2));

        Assert.Equal("b@example.com", rich.PrimaryEmail);
        Assert.Equal("C-000009", rich.Number);
        Assert.Equal("Najd", rich.DisplayName.En);
        Assert.Null(empty.PrimaryEmail);
        Assert.Null(empty.PrimaryPhone);
    }

    [Fact]
    public void TheOutcomeFactoriesSetCreatedAndTheCandidates()
    {
        CustomerDto dto = CustomerDto.FromDomain(Rich());
        DuplicateCandidateDto candidate = new(Ids.Customer1.Value, "C-000001", new CustomerNameDto("a", "b"), "Active", ["a@b.co"], []);

        CreateCustomerOutcome created = CreateCustomerOutcome.Success(dto);
        CreateCustomerOutcome duplicates = CreateCustomerOutcome.Duplicates([candidate]);

        Assert.True(created.Created);
        Assert.Same(dto, created.Customer);
        Assert.Empty(created.DuplicateCandidates);
        Assert.False(duplicates.Created);
        Assert.Null(duplicates.Customer);
        Assert.Equal(candidate, Assert.Single(duplicates.DuplicateCandidates));
    }

    [Fact]
    public void ACandidateIsAValueRecord()
    {
        var one = new DuplicateCandidateDto(Ids.Customer1.Value, "C-000001", new CustomerNameDto("a", "b"), "Active", ["x"], ["y"]);

        Assert.Equal(Ids.Customer1.Value, one.Id);
        Assert.Equal("Active", one.Status);
        Assert.Equal(["x"], one.MatchedEmails);
        Assert.Equal(["y"], one.MatchedPhones);
        Assert.Equal(one with { Number = "C-000001" }, one);
    }

    [Fact]
    public void EveryFromDomainRejectsNull()
    {
        Func<Customer, CustomerDto> customer = CustomerDto.FromDomain;
        Func<Customer, CustomerListItemDto> listItem = CustomerListItemDto.FromDomain;
        Func<Customer, CustomerLookupDto> lookup = CustomerLookupDto.FromDomain;
        Func<Contact, ContactDto> contact = ContactDto.FromDomain;
        Func<CustomerNote, CustomerNoteDto> note = CustomerNoteDto.FromDomain;
        Func<LocalizedText, CustomerNameDto> name = CustomerNameDto.FromDomain;

        NullArguments.AssertThrowsArgumentNull(customer, [null]);
        NullArguments.AssertThrowsArgumentNull(listItem, [null]);
        NullArguments.AssertThrowsArgumentNull(lookup, [null]);
        NullArguments.AssertThrowsArgumentNull(contact, [null]);
        NullArguments.AssertThrowsArgumentNull(note, [null]);
        NullArguments.AssertThrowsArgumentNull(name, [null]);
    }
}
