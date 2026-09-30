// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Sample.Application.Commands.CreateSample;

// DepartmentId is optional (Story 06): null resolves through OrganisationScope.ResolveWriteTarget - the
// caller's active scope header, or their single department when unambiguous.
[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record CreateSampleCommand(string Name, IReadOnlyList<string> Lines, Guid? DepartmentId) : ICommand<SampleAggregateDto>;
