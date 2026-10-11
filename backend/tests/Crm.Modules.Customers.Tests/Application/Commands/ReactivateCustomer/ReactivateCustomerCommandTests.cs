using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.ReactivateCustomer;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.ReactivateCustomer;

public sealed class ReactivateCustomerCommandHandlerTests
{
    private readonly Harness _harness = new();

    private ReactivateCustomerCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork, _harness.Clock, _harness.Bus);

    private static ReactivateCustomerCommand Command() => new(Ids.Customer1.Value, Ids.RowVersion());

    private static Customer Inactive()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        customer.Deactivate();
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
    public async Task TheCustomerIsReactivatedAfterTheRowVersionAndSavedBeforeTheEvent()
    {
        Customer customer = Inactive();
        _harness.Holding(customer);

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.Equal("Active", result.Value.Status);
        Assert.Equal(["rowversion", "save", "publish"], _harness.Log);
        _harness.Repository.Received(1).SetOriginalRowVersion(customer, Arg.Is<byte[]>(b => b.SequenceEqual(Ids.RowVersion())));
    }

    [Fact]
    public async Task TheUpdatedEventCarriesTheActiveStatus()
    {
        _harness.Holding(Inactive());

        await Handler().Handle(Command(), Stubs.Ct);

        Assert.Equal("Active", Assert.IsType<CustomerUpdatedIntegrationEvent>(Assert.Single(_harness.Published)).Status);
    }

    [Fact]
    public async Task AnAlreadyActiveCustomerIsA422WithoutASaveOrAnEvent()
    {
        _harness.Holding(CustomerFixtures.Individual(id: Ids.Customer1));

        await Expect.DomainCode(() => Handler().Handle(Command(), Stubs.Ct), "Customers.AlreadyActive");

        Assert.Equal(0, _harness.Saves);
        Assert.Empty(_harness.Published);
    }
}

public sealed class ReactivateCustomerCommandValidatorTests
{
    private readonly ReactivateCustomerCommandValidator _validator = new();

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(_validator.Validate(new ReactivateCustomerCommand(Ids.Customer1.Value, Ids.RowVersion())));

    [Fact]
    public void TheIdAndTheRowVersionAreRequired()
    {
        Expect.Invalid(_validator.Validate(new ReactivateCustomerCommand(Guid.Empty, Ids.RowVersion())), "Id");
        Expect.Invalid(_validator.Validate(new ReactivateCustomerCommand(Ids.Customer1.Value, [])), "RowVersion");
    }
}
