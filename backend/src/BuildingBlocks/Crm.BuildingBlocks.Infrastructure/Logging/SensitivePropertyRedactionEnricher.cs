using Serilog.Core;
using Serilog.Events;

namespace Crm.BuildingBlocks.Infrastructure.Logging;

/// <summary>
/// Redacts secret-named TOP-LEVEL properties (a scalar template such as <c>{ApiKey}</c>), which the
/// destructuring policy never sees. Registered last so it also covers properties other enrichers added.
/// </summary>
public sealed class SensitivePropertyRedactionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        foreach (string name in logEvent.Properties.Keys.Where(SensitiveData.IsSensitive).ToList())
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, SensitiveData.Redacted));
        }
    }
}
