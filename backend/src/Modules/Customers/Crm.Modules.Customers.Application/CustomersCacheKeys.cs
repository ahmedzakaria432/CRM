namespace Crm.Modules.Customers.Application;

/// <summary>Cache keys and tags: {module}:{aggregate-plural}:{id}.</summary>
public static class CustomersCacheKeys
{
    public static string Customer(Guid id) => $"customers:customers:{id}";
}
