namespace Crm.Api.Endpoints;

/// <summary>
/// One implementation per feature area's endpoint group, e.g. <c>SampleEndpoints</c>. Discovered by
/// assembly scan in <see cref="EndpointGroupExtensions.MapEndpointGroups"/> - implementing this
/// interface is enough to be wired into the route table, no manual registration needed.
/// </summary>
public interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder app);
}
