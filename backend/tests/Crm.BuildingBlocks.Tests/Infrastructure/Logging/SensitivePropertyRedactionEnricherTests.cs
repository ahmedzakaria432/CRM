using Crm.BuildingBlocks.Infrastructure.Logging;
using Crm.BuildingBlocks.Tests.Support;
using Serilog.Core;
using Serilog.Events;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Logging;

public sealed class SensitivePropertyRedactionEnricherTests
{
    private static Serilog.Core.Logger LoggerFor(CollectingSink sink) =>
        sink.Logger(c => c.Enrich.With(new SensitivePropertyRedactionEnricher()));

    [Fact]
    public void SecretNamedTopLevelPropertiesAreRedacted()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.Information("{Password} {apiKey} {Name}", "p", "k", "ada");

        Assert.Equal("***", CollectingSink.Scalar(sink.Only, "Password"));
        Assert.Equal("***", CollectingSink.Scalar(sink.Only, "apiKey"));
        Assert.Equal("ada", CollectingSink.Scalar(sink.Only, "Name"));
    }

    [Fact]
    public void SeparatorsInThePropertyNameDoNotHideASecret()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.ForContext("Bearer-Token", "abc").ForContext("X-Api-Key", "k").ForContext("client.secret", "s")
            .Information("hello");

        Assert.Equal("***", CollectingSink.Scalar(sink.Only, "Bearer-Token"));
        Assert.Equal("***", CollectingSink.Scalar(sink.Only, "X-Api-Key"));
        Assert.Equal("***", CollectingSink.Scalar(sink.Only, "client.secret"));
    }

    [Fact]
    public void OtherPropertiesAreUntouched()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.Information("{UserId} {Count}", "u-1", 5);

        Assert.Equal("u-1", CollectingSink.Scalar(sink.Only, "UserId"));
        Assert.Equal("5", CollectingSink.Scalar(sink.Only, "Count"));
    }

    [Fact]
    public void AnEventWithNoPropertiesIsSafe()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.Information("nothing to see");

        Assert.Empty(sink.Only.Properties);
    }

    [Fact]
    public void NullArgumentsAreRefused()
    {
        var enricher = new SensitivePropertyRedactionEnricher();
        var factory = Substitute.For<ILogEventPropertyFactory>();
        var logEvent = new LogEvent(Clock.Now, LogEventLevel.Information, null, MessageTemplate.Empty, []);

        NullArguments.AssertThrowsArgumentNull(enricher.Enrich, null, factory);
        NullArguments.AssertThrowsArgumentNull(enricher.Enrich, logEvent, null);
    }
}
