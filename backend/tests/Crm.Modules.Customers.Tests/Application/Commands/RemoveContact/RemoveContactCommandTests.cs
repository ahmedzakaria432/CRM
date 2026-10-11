using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.RemoveContact;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.RemoveContact;

public sealed class RemoveContactCommandHandlerTests
{
    private readonly Harness _harness = new();

    private RemoveContactCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork);

    private static RemoveContactCommand Command(Guid? contact = null) =>
        new(Ids.Customer1.Value, contact ?? Ids.Contact2, Ids.RowVersion());

    private Customer Holding(bool second = true)
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        customer.AddSimpleContact("First", "first@example.com", id: Ids.Contact1);
        if (second)
        {
            customer.AddSimpleContact("Second", "second@example.com", id: Ids.Contact2);
        }

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
    public async Task AnUnknownContactIsNotFoundWithoutARowVersionOrASave()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(Ids.Missing), Stubs.Ct);

        Expect.Failure(result, "Customers.ContactNotFound", ErrorType.NotFound);
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task ANonPrimaryContactIsRemovedAfterTheRowVersionAndOneSave()
    {
        Customer customer = Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.Equal(Ids.Contact1, Assert.Single(result.Value.Contacts).Id);
        Assert.Equal(["rowversion", "save"], _harness.Log);
        _harness.Repository.Received(1).SetOriginalRowVersion(customer, Arg.Is<byte[]>(b => b.SequenceEqual(Ids.RowVersion())));
    }

    [Fact]
    public async Task ThePrimaryWithOthersIsA422WithoutASave()
    {
        Holding();

        await Expect.DomainCode(() => Handler().Handle(Command(Ids.Contact1), Stubs.Ct), "Customers.PrimaryContactRemoval");

        Assert.Equal(0, _harness.Saves);
    }

    [Fact]
    public async Task TheLastContactCanBeRemoved()
    {
        Holding(second: false);

        Result<CustomerDto> result = await Handler().Handle(Command(Ids.Contact1), Stubs.Ct);

        Assert.Empty(result.Value.Contacts);
        Assert.Equal(1, _harness.Saves);
    }

    [Fact]
    public void TheValidatorRequiresBothIdsAndTheRowVersion()
    {
        var validator = new RemoveContactCommandValidator();

        Expect.Valid(validator.Validate(Command()));
        Expect.Invalid(validator.Validate(new RemoveContactCommand(Guid.Empty, Ids.Contact1, Ids.RowVersion())), "CustomerId");
        Expect.Invalid(validator.Validate(new RemoveContactCommand(Ids.Customer1.Value, Guid.Empty, Ids.RowVersion())), "ContactId");
        Expect.Invalid(validator.Validate(new RemoveContactCommand(Ids.Customer1.Value, Ids.Contact1, [])), "RowVersion");
    }
}
