using System.Reflection;

namespace Crm.Modules.Organisation.Presentation;

/// <summary>Handle on this assembly so the host can add it as an MVC application part.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
