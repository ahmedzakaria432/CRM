using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.AddNote;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.AddNote;

public sealed class AddNoteCommandHandlerTests
{
    private readonly Harness _harness = new(Clock.Later(30));

    private AddNoteCommandHandler Handler(string? userId = null, bool anonymous = false) =>
        new(
            _harness.Repository, _harness.UnitOfWork,
            anonymous ? CurrentUsers.WithUserId(Scopes.AgentA1(), null) : CurrentUsers.WithUserId(Scopes.AgentA1(), userId ?? CurrentUsers.DefaultUserId),
            _harness.Clock);

    private static AddNoteCommand Command(string body = "  Called the customer  ") => new(Ids.Customer1.Value, body);

    private Customer Holding()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        _harness.Holding(customer);
        return customer;
    }

    [Fact]
    public async Task AnUnknownCustomerIsNotFound()
    {
        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Customers.CustomerNotFound", ErrorType.NotFound);
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task TheNoteIsAppendedWithTheCurrentUserAsAuthorAndTheClockAsTimestamp()
    {
        Customer customer = Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        CustomerNoteDto note = Assert.Single(result.Value.Notes);
        Assert.Equal("Called the customer", note.Body);
        Assert.Equal(Ids.Author, note.AuthorId);
        Assert.Equal(Clock.Later(30), note.CreatedAtUtc);
        Assert.False(note.IsPinned);
        Assert.Equal(["save"], _harness.Log);
        Assert.Equal(0, customer.Revision);
    }

    [Fact]
    public async Task NoRowVersionIsSetBecauseNotesTakeNone()
    {
        Holding();

        await Handler().Handle(Command(), Stubs.Ct);

        _harness.Repository.DidNotReceive().SetOriginalRowVersion(Arg.Any<Customer>(), Arg.Any<byte[]>());
    }

    [Fact]
    public async Task NotesAreListedPinnedFirstThenNewestFirst()
    {
        Customer customer = Holding();
        customer.AddNote("old", Ids.Author, Clock.Later(1), Ids.Note1);
        customer.AddNote("pinned", Ids.Author, Clock.Later(2), Ids.Note2);
        customer.PinNote(Ids.Note2, Clock.Later(3));

        Result<CustomerDto> result = await Handler().Handle(Command("newest"), Stubs.Ct);

        Assert.Equal(["pinned", "newest", "old"], result.Value.Notes.Select(n => n.Body));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task AnUnusableUserIdMakesTheAuthorEmptyWhichTheAggregateRefuses(string? userId)
    {
        Holding();

        await Expect.DomainCode(() => Handler(userId, anonymous: userId is null).Handle(Command(), Stubs.Ct), "Customers.NoteAuthorRequired");

        Assert.Equal(0, _harness.Saves);
    }

    [Fact]
    public async Task AnEmptyBodyIsA422()
    {
        Holding();

        await Expect.DomainCode(() => Handler().Handle(Command("   "), Stubs.Ct), "Customers.NoteBodyRequired");
    }

    [Fact]
    public async Task ATooLongBodyIsA422()
    {
        Holding();

        await Expect.DomainCode(() => Handler().Handle(Command(new string('b', 4001)), Stubs.Ct), "Customers.NoteBodyTooLong");

        Assert.Equal(0, _harness.Saves);
    }
}

public sealed class AddNoteCommandValidatorTests
{
    private readonly AddNoteCommandValidator _validator = new();

    private ValidationResult Validate(Guid? customer = null, string body = "hi") =>
        _validator.Validate(new AddNoteCommand(customer ?? Ids.Customer1.Value, body));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void TheCustomerIsRequired() => Expect.Invalid(Validate(customer: Guid.Empty), "CustomerId");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankBodyIsInvalidBecauseNotEmptyRejectsWhitespace(string body) =>
        Expect.Invalid(Validate(body: body), "Body");

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void TheBodyLimitIsFourThousandCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(body: new string('b', length)).IsValid);
}
