using System.Reflection;

namespace Crm.Modules.Sample.Application;

/// <summary>Handle on this assembly for MediatR/FluentValidation scanning and unit-of-work lookup.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
