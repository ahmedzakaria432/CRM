using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Crm.BuildingBlocks.Application.Diagnostics;

/// <summary>The application's own OpenTelemetry source and meter, registered by AddCrmObservability.</summary>
public static class CrmDiagnostics
{
    public const string SourceName = "Crm";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    /// <summary>Every MediatR request, tagged crm.request.name / crm.request.kind (command|query) /
    /// crm.outcome (success|failure|exception).</summary>
    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("crm.request.duration", unit: "ms", description: "MediatR request duration");
}
