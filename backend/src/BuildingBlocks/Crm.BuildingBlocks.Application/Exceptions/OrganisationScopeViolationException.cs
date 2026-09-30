namespace Crm.BuildingBlocks.Application.Exceptions;

/// <summary>A read or write touched a row outside the caller's organisation scope. Thrown by the repository
/// (read of an existing out-of-scope row) and by ModuleDbContext's write guard; turned into 403 by
/// OrganisationScopeBehavior (and GlobalExceptionHandler as a backstop).</summary>
public sealed class OrganisationScopeViolationException(string entityType, string operation)
    : Exception($"{operation} of a {entityType} outside the caller's organisation scope.")
{
    public string EntityType { get; } = entityType;

    public string Operation { get; } = operation; // "Read" or "Write"
}
