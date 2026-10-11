using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.PinNote;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.PinNote;

public sealed class PinNoteCommandHandlerTests
{
    private readonly Harness _harness = new(Clock.Later(45));

    private PinNoteCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork, _harness.Clock);

    private static PinNoteCommand Command(bool pinned = true, Guid? note = null) => new(Ids.Customer1.Value, note ?? Ids.Note1, pinned);

    private Customer Holding()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        customer.AddNote("a", Ids.Author, Clock.Later(1), Ids.Note1);
        customer.AddNote("b", Ids.Author, Clock.Later(2), Ids.Note2);
        _harness.Holding(customer);
        return customer;
    }

    [Fact]
    public async Task AnUnknownCustomerIsNotFound()
    {
        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Customers.CustomerNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task AnUnknownNoteIsNotFoundWithoutASave()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(note: Ids.Missing), Stubs.Ct);

        Expect.Failure(result, "Customers.NoteNotFound", ErrorType.NotFound);
        Assert.Contains(Ids.Missing.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task PinningStampsTheClockAndListsTheNoteFirst()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(note: Ids.Note1), Stubs.Ct);

        CustomerNoteDto first = result.Value.Notes[0];
        Assert.Equal(Ids.Note1, first.Id);
        Assert.True(first.IsPinned);
        Assert.Equal(Clock.Later(45), first.PinnedAtUtc);
        Assert.Equal(["save"], _harness.Log);
    }

    [Fact]
    public async Task PinningIsIdempotentButStillSaves()
    {
        Customer customer = Holding();
        customer.PinNote(Ids.Note1, Clock.Later(3));

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.Equal(Clock.Later(3), result.Value.Notes.Single(n => n.Id == Ids.Note1).PinnedAtUtc);
        Assert.Equal(1, _harness.Saves);
    }

    [Fact]
    public async Task UnpinningClearsThePinAndAnUnpinnedNoteIsAccepted()
    {
        Customer customer = Holding();
        customer.PinNote(Ids.Note1, Clock.Later(3));

        Result<CustomerDto> result = await Handler().Handle(Command(pinned: false), Stubs.Ct);
        Result<CustomerDto> again = await Handler().Handle(Command(pinned: false), Stubs.Ct);

        CustomerNoteDto note = result.Value.Notes.Single(n => n.Id == Ids.Note1);
        Assert.False(note.IsPinned);
        Assert.Null(note.PinnedAtUtc);
        Assert.True(again.IsSuccess);
    }

    [Fact]
    public async Task NoRowVersionIsSetAndNoEventIsPublished()
    {
        Holding();

        await Handler().Handle(Command(), Stubs.Ct);

        _harness.Repository.DidNotReceive().SetOriginalRowVersion(Arg.Any<Customer>(), Arg.Any<byte[]>());
        Assert.Empty(_harness.Published);
    }

    [Fact]
    public void TheValidatorRequiresBothIds()
    {
        var validator = new PinNoteCommandValidator();

        Expect.Valid(validator.Validate(Command()));
        Expect.Invalid(validator.Validate(new PinNoteCommand(Guid.Empty, Ids.Note1, true)), "CustomerId");
        Expect.Invalid(validator.Validate(new PinNoteCommand(Ids.Customer1.Value, Guid.Empty, true)), "NoteId");
    }
}
