using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Crm.BuildingBlocks.Infrastructure.Logging;

/// <summary>
/// Redacts secret-named properties of any object logged with <c>{@X}</c>, at every nesting depth. Nested
/// values go back through the factory, so this policy runs on them too (verified by probe while planning
/// Story 03, through a [LoggerMessage] template).
/// </summary>
public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string
            || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            result = null;
            return false;
        }

        IEnumerable<LogEventProperty> properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p => new LogEventProperty(
                p.Name,
                SensitiveData.IsSensitive(p.Name)
                    ? new ScalarValue(SensitiveData.Redacted)
                    : propertyValueFactory.CreatePropertyValue(p.GetValue(value), destructureObjects: true)));

        result = new StructureValue(properties, type.Name);
        return true;
    }
}
