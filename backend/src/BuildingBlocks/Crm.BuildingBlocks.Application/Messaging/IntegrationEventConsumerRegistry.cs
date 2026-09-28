using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Crm.BuildingBlocks.Application.Messaging;

/// <summary>Every subscribed integration event type and its handlers, keyed by the event type's FullName (the
/// value stored in an outbox row's Type column).</summary>
public sealed record IntegrationEventSubscription(Type EventType, IReadOnlyList<Type> HandlerTypes);

/// <summary>Built once at startup from the module Application assemblies the host registers.</summary>
public sealed class IntegrationEventConsumerRegistry
{
    private readonly Dictionary<string, IntegrationEventSubscription> _subscriptions;

    private IntegrationEventConsumerRegistry(Dictionary<string, IntegrationEventSubscription> subscriptions) =>
        _subscriptions = subscriptions;

    public IReadOnlyCollection<Type> HandlerTypes => [.. _subscriptions.Values.SelectMany(s => s.HandlerTypes).Distinct()];

    public bool TryGet(string eventTypeName, [MaybeNullWhen(false)] out IntegrationEventSubscription subscription) =>
        _subscriptions.TryGetValue(eventTypeName, out subscription);

    public static IntegrationEventConsumerRegistry Scan(IEnumerable<Assembly> assemblies)
    {
        var handlersByEvent = new Dictionary<Type, List<Type>>();

        foreach (Type type in assemblies.SelectMany(a => a.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (Type contract in type.GetInterfaces().Where(i =>
                         i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventConsumer<>)))
            {
                Type eventType = contract.GetGenericArguments()[0];
                if (!handlersByEvent.TryGetValue(eventType, out List<Type>? handlers))
                {
                    handlers = [];
                    handlersByEvent[eventType] = handlers;
                }

                handlers.Add(type);
            }
        }

        return new IntegrationEventConsumerRegistry(handlersByEvent.ToDictionary(
            pair => pair.Key.FullName ?? pair.Key.Name,
            pair => new IntegrationEventSubscription(pair.Key, pair.Value),
            StringComparer.Ordinal));
    }
}
