using System.Reflection;

namespace Crm.Api.Endpoints;

public static class EndpointGroupExtensions
{
    /// <summary>
    /// Discovers every concrete <see cref="IEndpointGroup"/> in this assembly and maps it. A new
    /// feature's endpoint group needs no manual registration here - implementing the interface is enough.
    /// </summary>
    public static void MapEndpointGroups(this WebApplication app)
    {
        IEnumerable<IEndpointGroup> groups = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEndpointGroup).IsAssignableFrom(t))
            .Select(t => (IEndpointGroup)Activator.CreateInstance(t)!);

        foreach (IEndpointGroup group in groups)
        {
            group.Map(app);
        }
    }
}
