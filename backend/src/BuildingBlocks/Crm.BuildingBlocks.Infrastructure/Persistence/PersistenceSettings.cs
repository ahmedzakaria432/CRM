namespace Crm.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Host-level persistence switches shared by every module DbContext.</summary>
public sealed record PersistenceSettings(bool EnableSensitiveDataLogging);
