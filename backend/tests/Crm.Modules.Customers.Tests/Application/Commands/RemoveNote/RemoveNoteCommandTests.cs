using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.RemoveNote;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.RemoveNote;

public sealed class RemoveNoteCommandHandlerTests
{
    private readonly Harness _harness = new();

    private RemoveNoteCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork);

    private static RemoveNoteCommand Command(Guid? note = null) => new(Ids.Customer1.Value, note ?? Ids.Note1);

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

        Result<CustomerDto> result = await Handler().Handle(Command(Ids.Missing), Stubs.Ct);

        Expect.Failure(result, "Customers.NoteNotFound", ErrorType.NotFound);
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task OnlyThatNoteIsRemovedAndTheSaveHappensOnceWithoutARowVersion()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.Equal(Ids.Note2, Assert.Single(result.Value.Notes).Id);
        Assert.Equal(["save"], _harness.Log);
    }

    [Fact]
    public async Task RemovingTheSameNoteTwiceFailsTheSecondTime()
    {
        Holding();

        await Handler().Handle(Command(), Stubs.Ct);
        Result<CustomerDto> second = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(second, "Customers.NoteNotFound", ErrorType.NotFound);
        Assert.Equal(1, _harness.Saves);
    }

    [Fact]
    public void TheValidatorRequiresBothIds()
    {
        var validator = new RemoveNoteCommandValidator();

        Expect.Valid(validator.Validate(Command()));
        Expect.Invalid(validator.Validate(new RemoveNoteCommand(Guid.Empty, Ids.Note1)), "CustomerId");
        Expect.Invalid(validator.Validate(new RemoveNoteCommand(Ids.Customer1.Value, Guid.Empty)), "NoteId");
    }
}
