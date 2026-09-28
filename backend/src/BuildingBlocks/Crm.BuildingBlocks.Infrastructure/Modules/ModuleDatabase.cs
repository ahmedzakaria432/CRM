namespace Crm.BuildingBlocks.Infrastructure.Modules;

/// <summary>One registration per module DbContext, added by <c>AddModuleDbContext</c>. Lets the host
/// migrate every module in Development without knowing any module's DbContext type.</summary>
public sealed record ModuleDatabase(string ModuleName, string Schema, Type DbContextType);
