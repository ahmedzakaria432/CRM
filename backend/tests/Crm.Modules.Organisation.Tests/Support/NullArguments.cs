using System.Reflection;

namespace Crm.Modules.Organisation.Tests.Support;

/// <summary>Passing a literal null to a non-nullable parameter does not compile warning-free, and the test projects
/// may neither suppress the warning nor use the null-forgiving operator. Invoking the method through its delegate
/// reaches the same guard clause without either.</summary>
public static class NullArguments
{
    public static void AssertThrowsArgumentNull(Delegate method, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(method);

        var invocation = Assert.Throws<TargetInvocationException>(() => method.DynamicInvoke(arguments));

        Assert.IsType<ArgumentNullException>(invocation.InnerException);
    }

    /// <summary>For a method that returns a task: the guard may throw straight away or inside the task.</summary>
    public static async Task AssertThrowsArgumentNullAsync(Delegate method, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(method);

        object? returned;
        try
        {
            returned = method.DynamicInvoke(arguments);
        }
        catch (TargetInvocationException invocation)
        {
            Assert.IsType<ArgumentNullException>(invocation.InnerException);
            return;
        }

        var task = Assert.IsAssignableFrom<Task>(returned);
        await Assert.ThrowsAsync<ArgumentNullException>(() => task);
    }

    public static string? ParamNameOfNullFailure(Delegate method, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(method);

        var invocation = Assert.Throws<TargetInvocationException>(() => method.DynamicInvoke(arguments));

        return Assert.IsType<ArgumentNullException>(invocation.InnerException).ParamName;
    }
}
