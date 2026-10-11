using System.Reflection;
using System.Reflection.Emit;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>Emits a throw-away assembly holding deliberately wrong MediatR requests. RequestAuthorizationGuard scans
/// whole assemblies, so a failing scenario needs an assembly of its own; the test assembly itself is never passed.</summary>
public sealed class DynamicAssemblyBuilder
{
    private readonly AssemblyBuilder _assembly = AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("Crm.DynamicRequests." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);

    private readonly ModuleBuilder _module;

    public DynamicAssemblyBuilder()
    {
        _module = _assembly.DefineDynamicModule("Main");
    }

    public Assembly Assembly => _assembly;

    public static CustomAttributeBuilder RequirePermission(string permission) =>
        new(typeof(RequirePermissionAttribute).GetConstructor([typeof(string)]) ?? throw new InvalidOperationException(), [permission]);

    public static CustomAttributeBuilder RequireAuthenticatedUser() =>
        new(typeof(RequireAuthenticatedUserAttribute).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException(), []);

    public static CustomAttributeBuilder AllowAnonymous() =>
        new(typeof(AllowAnonymousRequestAttribute).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException(), []);

    /// <summary>A public sealed class implementing <c>IRequest&lt;Result&gt;</c> (or the given response type).</summary>
    public Type Request(string name, Type? response = null, params CustomAttributeBuilder[] attributes) =>
        Define(name, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, response ?? typeof(Result), false, attributes);

    public Type AbstractRequest(string name, params CustomAttributeBuilder[] attributes) =>
        Define(name, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract, typeof(Result), false, attributes);

    public Type OpenGenericRequest(string name, params CustomAttributeBuilder[] attributes) =>
        Define(name, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(Result), true, attributes);

    public Type InterfaceRequest(string name)
    {
        TypeBuilder builder = _module.DefineType(
            name, TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract, null, [typeof(IRequest<Result>)]);
        return builder.CreateType();
    }

    private Type Define(string name, TypeAttributes attributes, Type response, bool openGeneric, CustomAttributeBuilder[] custom)
    {
        Type contract = typeof(IRequest<>).MakeGenericType(response);
        TypeBuilder builder = _module.DefineType(name, attributes, typeof(object), [contract]);

        if (openGeneric)
        {
            builder.DefineGenericParameters("T");
        }

        foreach (CustomAttributeBuilder attribute in custom)
        {
            builder.SetCustomAttribute(attribute);
        }

        return builder.CreateType();
    }
}
