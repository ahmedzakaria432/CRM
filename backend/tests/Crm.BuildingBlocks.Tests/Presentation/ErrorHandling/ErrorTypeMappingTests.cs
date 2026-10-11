using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Crm.SharedKernel.Results;

namespace Crm.BuildingBlocks.Tests.Presentation.ErrorHandling;

public sealed class ErrorTypeMappingTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400, "urn:crm:problem:validation")]
    [InlineData(ErrorType.NotFound, 404, "urn:crm:problem:not-found")]
    [InlineData(ErrorType.Conflict, 409, "urn:crm:problem:conflict")]
    [InlineData(ErrorType.Forbidden, 403, "urn:crm:problem:forbidden")]
    [InlineData(ErrorType.Domain, 422, "urn:crm:problem:domain-rule")]
    [InlineData(ErrorType.Unauthorized, 401, "urn:crm:problem:unauthorized")]
    [InlineData(ErrorType.Failure, 500, "urn:crm:problem:internal")]
    public void EveryErrorTypeMapsToItsStatusAndProblemType(ErrorType type, int status, string problemType)
    {
        Assert.Equal(status, ErrorTypeMapping.ToStatusCode(type));
        Assert.Equal(problemType, ErrorTypeMapping.ToProblemType(type));
    }

    [Fact]
    public void AnUnknownErrorTypeFallsBackToInternalServerError()
    {
        Assert.Equal(500, ErrorTypeMapping.ToStatusCode((ErrorType)99));
        Assert.Equal(ProblemTypes.Internal, ErrorTypeMapping.ToProblemType((ErrorType)99));
    }
}
