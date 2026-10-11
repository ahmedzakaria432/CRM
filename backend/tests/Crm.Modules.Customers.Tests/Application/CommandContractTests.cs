using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.AddContact;
using Crm.Modules.Customers.Application.Commands.AddNote;
using Crm.Modules.Customers.Application.Commands.CreateCustomer;
using Crm.Modules.Customers.Application.Commands.DeactivateCustomer;
using Crm.Modules.Customers.Application.Commands.PinNote;
using Crm.Modules.Customers.Application.Commands.ReactivateCustomer;
using Crm.Modules.Customers.Application.Commands.RemoveContact;
using Crm.Modules.Customers.Application.Commands.RemoveNote;
using Crm.Modules.Customers.Application.Commands.SetPrimaryContact;
using Crm.Modules.Customers.Application.Commands.UpdateContact;
using Crm.Modules.Customers.Application.Commands.UpdateCustomer;
using Crm.Modules.Customers.Tests.Support;

namespace Crm.Modules.Customers.Tests.Application;

/// <summary>The contract every Customers command honours: write permission, the shared cache tag, the rowversion
/// on profile/status/contact commands only, and no bare unit of work in a handler.</summary>
public sealed class CommandContractTests
{
    private static readonly Guid Id = Ids.Customer1.Value;
    private static readonly byte[] Version = Ids.RowVersion();

    private static readonly object[] Commands =
    [
        new CreateCustomerCommand("a", "a", "Individual", null, "en", [], false, null),
        new UpdateCustomerCommand(Id, "a", "a", "Individual", null, "en", Version),
        new DeactivateCustomerCommand(Id, Version),
        new ReactivateCustomerCommand(Id, Version),
        new AddContactCommand(Id, "a", null, "a@b.co", null, false, false, Version),
        new UpdateContactCommand(Id, Ids.Contact1, "a", null, "a@b.co", null, false, Version),
        new SetPrimaryContactCommand(Id, Ids.Contact1, Version),
        new RemoveContactCommand(Id, Ids.Contact1, Version),
        new AddNoteCommand(Id, "body"),
        new PinNoteCommand(Id, Ids.Note1, true),
        new RemoveNoteCommand(Id, Ids.Note1),
    ];

    public static TheoryData<Type> CommandTypes => [.. Commands.Select(c => c.GetType())];

    public static TheoryData<Type> CacheInvalidating => [.. Commands.Select(c => c.GetType()).Where(t => t != typeof(CreateCustomerCommand))];

    private static object Instance(Type type) => Commands.Single(c => c.GetType() == type);

    private static Type HandlerOf(Type command) =>
        typeof(CustomersCacheKeys).Assembly.GetTypes().Single(t => t.Name == command.Name + "Handler");

    [Fact]
    public void ThereAreElevenCommands() => Assert.Equal(11, Commands.Length);

    [Theory]
    [MemberData(nameof(CommandTypes))]
    public void EveryCommandRequiresTheCustomersWritePermission(Type command) =>
        Assert.Equal("customers.write", Stubs.PermissionOf(command));

    [Theory]
    [MemberData(nameof(CommandTypes))]
    public void EveryCommandGoesThroughTheCommandPipeline(Type command) =>
        Assert.Contains(command.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

    [Theory]
    [MemberData(nameof(CacheInvalidating))]
    public void EveryCommandOnAnExistingCustomerInvalidatesThatCustomersTag(Type command)
    {
        var invalidating = Assert.IsAssignableFrom<ICacheInvalidatingCommand>(Instance(command));

        Assert.Equal([$"customers:customers:{Id}"], invalidating.CacheTagsToInvalidate);
    }

    [Fact]
    public void CreateCustomerDoesNotInvalidateAnything() =>
        Assert.False(Instance(typeof(CreateCustomerCommand)) is ICacheInvalidatingCommand);

    [Theory]
    [InlineData(typeof(UpdateCustomerCommand), true)]
    [InlineData(typeof(DeactivateCustomerCommand), true)]
    [InlineData(typeof(ReactivateCustomerCommand), true)]
    [InlineData(typeof(AddContactCommand), true)]
    [InlineData(typeof(UpdateContactCommand), true)]
    [InlineData(typeof(SetPrimaryContactCommand), true)]
    [InlineData(typeof(RemoveContactCommand), true)]
    [InlineData(typeof(CreateCustomerCommand), false)]
    [InlineData(typeof(AddNoteCommand), false)]
    [InlineData(typeof(PinNoteCommand), false)]
    [InlineData(typeof(RemoveNoteCommand), false)]
    public void OnlyProfileStatusAndContactCommandsCarryARowVersion(Type command, bool carries) =>
        Assert.Equal(carries, command.GetProperty("RowVersion") is not null);

    [Theory]
    [MemberData(nameof(CommandTypes))]
    public void NoHandlerTakesTheBareUnitOfWork(Type command)
    {
        Type[] parameters = [.. HandlerOf(command).GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];

        Assert.DoesNotContain(typeof(IUnitOfWork), parameters);
        Assert.Contains(typeof(ICustomersUnitOfWork), parameters);
    }

    [Theory]
    [InlineData(typeof(CreateCustomerCommand), true)]
    [InlineData(typeof(UpdateCustomerCommand), true)]
    [InlineData(typeof(DeactivateCustomerCommand), true)]
    [InlineData(typeof(ReactivateCustomerCommand), true)]
    [InlineData(typeof(AddContactCommand), false)]
    [InlineData(typeof(UpdateContactCommand), false)]
    [InlineData(typeof(SetPrimaryContactCommand), false)]
    [InlineData(typeof(RemoveContactCommand), false)]
    [InlineData(typeof(AddNoteCommand), false)]
    [InlineData(typeof(PinNoteCommand), false)]
    [InlineData(typeof(RemoveNoteCommand), false)]
    public void OnlyTheCreateAndProfileAndStatusHandlersPublishEvents(Type command, bool publishes)
    {
        bool takesBus = HandlerOf(command).GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IEventBus));

        Assert.Equal(publishes, takesBus);
    }

    [Theory]
    [InlineData(typeof(AddNoteCommand), true, true)]
    [InlineData(typeof(PinNoteCommand), false, true)]
    [InlineData(typeof(RemoveNoteCommand), false, false)]
    [InlineData(typeof(AddContactCommand), false, false)]
    public void ClockAndCurrentUserAreInjectedOnlyWhereTheyAreUsed(Type command, bool takesUser, bool takesClock)
    {
        Type[] parameters = [.. HandlerOf(command).GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];

        Assert.Equal(takesUser, parameters.Contains(typeof(ICurrentUserService)));
        Assert.Equal(takesClock, parameters.Contains(typeof(IDateTime)));
    }

    [Fact]
    public void EveryCommandHasItsOwnPermissionAttributeOnTheRecord() =>
        Assert.All(Commands, c => Assert.NotNull(c.GetType().GetCustomAttribute<RequirePermissionAttribute>(inherit: false)));
}
