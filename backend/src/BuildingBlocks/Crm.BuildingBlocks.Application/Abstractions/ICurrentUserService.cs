namespace Crm.BuildingBlocks.Application.Abstractions;

public interface ICurrentUserService
{
    string? UserId { get; }

    /// <summary>"system" when unauthenticated - seeder, background jobs, migrations.</summary>
    string UserIdOrSystem { get; }
}
