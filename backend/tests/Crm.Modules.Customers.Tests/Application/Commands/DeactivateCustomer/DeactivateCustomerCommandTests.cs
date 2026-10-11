using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.DeactivateCustomer;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.DeactivateCustomer;

public sealed class DeactivateCustomerCommandHandlerTests
{
    private readonly Harness _harness = new();

    private DeactivateCustomerCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork, _harness.Clock, _harness.Bus);

    private static DeactivateCustomerCommand Command() => new(Ids.Customer1.Value, Ids.RowVersion());

    [Fact]
    public async Task AnUnknownCustomerIsNotFound()
    {
        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Customers.CustomerNotFound", ErrorType.NotFound);
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task TheCustomerIsDeactivatedAfterTheRowVersionAndSavedBeforeTheEvent()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        _harness.Holding(customer);

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.Equal("Inactive", result.Value.Status);
        Assert.Equal(CustomerStatus.Inactive, customer.Status);
        Assert.Equal(["rowversion", "save", "publish"], _harness.Log);
        _harness.Repository.Received(1).SetOriginalRowVersion(customer, Arg.Is<byte[]>(b => b.SequenceEqual(Ids.RowVersion())));
    }

    [Fact]
    public async Task TheUpdatedEventCarriesTheInactiveStatus()
    {
        _harness.Holding(CustomerFixtures.Individual(id: Ids.Customer1));

        await Handler().Handle(Command(), Stubs.Ct);

        CustomerUpdatedIntegrationEvent published = Assert.IsType<CustomerUpdatedIntegrationEvent>(Assert.Single(_harness.Published));
        Assert.Equal("Inactive", published.Status);
        Assert.Equal(Ids.Customer1.Value, published.CustomerId);
        Assert.Equal(Clock.Now, published.OccurredOnUtc);
    }

    [Fact]
    public async Task AnAlreadyInactiveCustomerIsA422WithoutASaveOrAnEvent()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        customer.Deactivate();
        _harness.Holding(customer);

        await Expect.DomainCode(() => Handler().Handle(Command(), Stubs.Ct), "Customers.AlreadyInactive");

        Assert.Equal(0, _harness.Saves);
        Assert.Empty(_harness.Published);
    }
}

public sealed class DeactivateCustomerCommandValidatorTests
{
    private readonly DeactivateCustomerCommandValidator _validator = new();

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(_validator.Validate(new DeactivateCustomerCommand(Ids.Customer1.Value, Ids.RowVersion())));

    [Fact]
    public void TheIdAndTheRowVersionAreRequired()
    {
        Expect.Invalid(_validator.Validate(new DeactivateCustomerCommand(Guid.Empty, Ids.RowVersion())), "Id");
        Expect.Invalid(_validator.Validate(new DeactivateCustomerCommand(Ids.Customer1.Value, [])), "RowVersion");
    }
}
