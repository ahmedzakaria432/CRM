using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.SetPrimaryContact;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.SetPrimaryContact;

public sealed class SetPrimaryContactCommandHandlerTests
{
    private readonly Harness _harness = new();

    private SetPrimaryContactCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork);

    private static SetPrimaryContactCommand Command(Guid? contact = null) =>
        new(Ids.Customer1.Value, contact ?? Ids.Contact2, Ids.RowVersion());

    private Customer Holding()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        customer.AddSimpleContact("First", "first@example.com", id: Ids.Contact1);
        customer.AddSimpleContact("Second", "second@example.com", id: Ids.Contact2);
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
    public async Task AnUnknownContactIsNotFoundWithoutARowVersionOrASave()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(Ids.Missing), Stubs.Ct);

        Expect.Failure(result, "Customers.ContactNotFound", ErrorType.NotFound);
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task TheTargetBecomesTheOnlyPrimaryAfterTheRowVersionAndOneSave()
    {
        Customer customer = Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        ContactDto primary = Assert.Single(result.Value.Contacts, c => c.IsPrimary);
        Assert.Equal(Ids.Contact2, primary.Id);
        Assert.Equal(Ids.Contact2, result.Value.Contacts[0].Id);
        Assert.Equal(["rowversion", "save"], _harness.Log);
        _harness.Repository.Received(1).SetOriginalRowVersion(customer, Arg.Is<byte[]>(b => b.SequenceEqual(Ids.RowVersion())));
    }

    [Fact]
    public async Task AnAlreadyPrimaryContactIsAccepted()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(Ids.Contact1), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _harness.Saves);
    }

    [Fact]
    public void TheValidatorRequiresBothIdsAndTheRowVersion()
    {
        var validator = new SetPrimaryContactCommandValidator();

        Expect.Valid(validator.Validate(Command()));
        Expect.Invalid(validator.Validate(new SetPrimaryContactCommand(Guid.Empty, Ids.Contact1, Ids.RowVersion())), "CustomerId");
        Expect.Invalid(validator.Validate(new SetPrimaryContactCommand(Ids.Customer1.Value, Guid.Empty, Ids.RowVersion())), "ContactId");
        Expect.Invalid(validator.Validate(new SetPrimaryContactCommand(Ids.Customer1.Value, Ids.Contact1, [])), "RowVersion");
    }
}
