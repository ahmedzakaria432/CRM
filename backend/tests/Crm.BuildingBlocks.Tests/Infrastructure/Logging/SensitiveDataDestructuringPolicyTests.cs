using Crm.BuildingBlocks.Infrastructure.Logging;
using Crm.BuildingBlocks.Tests.Support;
using Serilog.Core;
using Serilog.Events;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Logging;

public sealed class SensitiveDataDestructuringPolicyTests
{
    private delegate bool TryDestructure(object value, ILogEventPropertyValueFactory factory, out LogEventPropertyValue? result);

    private enum Color
    {
        Red,
    }

    private sealed class Account
    {
        public string Name { get; init; } = "ada";

        public string Password { get; init; } = "hunter2";

        public string RefreshToken { get; init; } = "r-token";

        public string ApiKey { get; init; } = "k";

        public string ClientSecret { get; init; } = "s";

        public string Authorization { get; init; } = "Bearer x";

        public string ConnectionString { get; init; } = "Server=x";

        public string Credentials { get; init; } = "c";

        public int Age { get; init; } = 36;
    }

    private sealed class Separated
    {
        public string pass_word { get; init; } = "p";

        public string Api_Key { get; init; } = "k";

        public string Visible { get; init; } = "v";
    }

    private sealed class Outer
    {
        public string Title { get; init; } = "outer";

        public Account Inner { get; init; } = new();

        public List<Account> Many { get; init; } = [new()];
    }

    private sealed class WithIndexer
    {
        public string Name { get; init; } = "n";

        public string this[int index] => index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Serilog.Core.Logger LoggerFor(CollectingSink sink) =>
        sink.Logger(c => c.Destructure.With<SensitiveDataDestructuringPolicy>());

    private static ILogEventPropertyValueFactory ScalarFactory()
    {
        var factory = Substitute.For<ILogEventPropertyValueFactory>();
        factory.CreatePropertyValue(Arg.Any<object?>(), Arg.Any<bool>()).Returns(call => new ScalarValue(call.ArgAt<object?>(0)));
        return factory;
    }

    private static Dictionary<string, LogEventPropertyValue> PropertiesOf(CollectingSink sink, string name = "Payload")
    {
        var structure = Assert.IsType<StructureValue>(sink.Only.Properties[name]);
        return structure.Properties.ToDictionary(p => p.Name, p => p.Value);
    }

    private static string? Text(LogEventPropertyValue value) => Assert.IsType<ScalarValue>(value).Value?.ToString();

    [Fact]
    public void SecretNamedPropertiesAreRedactedAndOthersKept()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.Information("{@Payload}", new Account());
        var properties = PropertiesOf(sink);

        foreach (string secret in new[] { "Password", "RefreshToken", "ApiKey", "ClientSecret", "Authorization", "ConnectionString", "Credentials" })
        {
            Assert.Equal("***", Text(properties[secret]));
        }

        Assert.Equal("ada", Text(properties["Name"]));
        Assert.Equal("36", Text(properties["Age"]));
    }

    [Fact]
    public void MatchingIgnoresSeparatorsAndCase()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.Information("{@Payload}", new Separated());
        var properties = PropertiesOf(sink);

        Assert.Equal("***", Text(properties["pass_word"]));
        Assert.Equal("***", Text(properties["Api_Key"]));
        Assert.Equal("v", Text(properties["Visible"]));
    }

    [Fact]
    public void NestedObjectsAreRedactedAtEveryDepth()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.Information("{@Payload}", new Outer());
        var outer = PropertiesOf(sink);

        Assert.Equal("outer", Text(outer["Title"]));
        var inner = Assert.IsType<StructureValue>(outer["Inner"]).Properties.ToDictionary(p => p.Name, p => p.Value);
        Assert.Equal("***", Text(inner["Password"]));
        Assert.Equal("ada", Text(inner["Name"]));
        var many = Assert.IsType<SequenceValue>(outer["Many"]);
        var first = Assert.IsType<StructureValue>(Assert.Single(many.Elements)).Properties.ToDictionary(p => p.Name, p => p.Value);
        Assert.Equal("***", Text(first["ApiKey"]));
    }

    [Fact]
    public void TheTypeTagIsTheTypeName()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.Information("{@Payload}", new Account());

        Assert.Equal(nameof(Account), Assert.IsType<StructureValue>(sink.Only.Properties["Payload"]).TypeTag);
    }

    [Fact]
    public void IndexersAreSkipped()
    {
        var sink = new CollectingSink();
        using var logger = LoggerFor(sink);

        logger.Information("{@Payload}", new WithIndexer());
        var properties = PropertiesOf(sink);

        Assert.Equal(["Name"], properties.Keys);
    }

    [Fact]
    public void PrimitivesStringsEnumsAndSystemTypesAreNotHandled()
    {
        var policy = new SensitiveDataDestructuringPolicy();
        var factory = ScalarFactory();

        foreach (object value in new object[] { 42, true, "text", Color.Red, Guid.Empty, new Uri("http://example.test/"), new List<int>() })
        {
            Assert.False(policy.TryDestructure(value, factory, out var result), value.GetType().Name);
            Assert.Null(result);
        }
    }

    [Fact]
    public void AnApplicationObjectIsHandledAndReturnsAStructure()
    {
        var policy = new SensitiveDataDestructuringPolicy();

        var handled = policy.TryDestructure(new Account(), ScalarFactory(), out var result);

        Assert.True(handled);
        Assert.IsType<StructureValue>(result);
    }

    [Fact]
    public void NullArgumentsAreRefused()
    {
        var policy = new SensitiveDataDestructuringPolicy();
        TryDestructure method = policy.TryDestructure;
        var factory = ScalarFactory();

        NullArguments.AssertThrowsArgumentNull(method, null, factory, null);
        NullArguments.AssertThrowsArgumentNull(method, new Account(), null, null);
    }

    [Fact]
    public void WriteOnlyPropertiesAreSkipped()
    {
        var policy = new SensitiveDataDestructuringPolicy();

        Assert.True(policy.TryDestructure(new WriteOnly(), ScalarFactory(), out var result));
        Assert.Empty(Assert.IsType<StructureValue>(result).Properties);
    }

    private sealed class WriteOnly
    {
        private string _value = string.Empty;

        public string Value
        {
            set => _value = value;
        }

        public override string ToString() => _value;
    }
}
