// CANONICAL SLICE - implement a module's Contracts API by sending the module's own queries.
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.Modules.Sample.Application.Queries.GetSampleById;
using Crm.Modules.Sample.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application;

/// <summary>Goes through the module's own query, so a cross-module read passes the same validation,
/// authorisation, and scoping pipeline as an HTTP read - there is no back door into the module.</summary>
public sealed class SampleModuleApi(ISender sender) : ISampleModuleApi
{
    public async Task<Result<SampleSummary>> GetSampleAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        Result<SampleAggregateDto> result = await sender.Send(new GetSampleByIdQuery(sampleId), cancellationToken);

        return result.IsSuccess
            ? Result.Success(new SampleSummary(result.Value.Id, result.Value.Name))
            : Result.Failure<SampleSummary>(result.Error);
    }
}
