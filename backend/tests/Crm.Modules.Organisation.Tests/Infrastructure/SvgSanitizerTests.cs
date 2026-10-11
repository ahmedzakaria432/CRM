using System.Text;
using System.Xml;
using System.Xml.Linq;
using Crm.Modules.Organisation.Infrastructure.Theming;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Infrastructure;

public sealed class SvgSanitizerTests
{
    private const string Ns = "xmlns=\"http://www.w3.org/2000/svg\"";
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private readonly SvgSanitizer _sanitizer = new();

    private byte[]? SanitizeBytes(string xml) => _sanitizer.Sanitize(Encoding.UTF8.GetBytes(xml));

    private XElement Clean(string xml)
    {
        byte[]? output = SanitizeBytes(xml);
        Assert.NotNull(output);
        return XDocument.Parse(Encoding.UTF8.GetString(output)).Root ?? throw new InvalidOperationException("no root");
    }

    private static string Wrap(string inner) => $"<svg {Ns}>{inner}</svg>";

    public static TheoryData<string> AllowedElementNames() =>
    [
        "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text", "tspan", "defs",
        "linearGradient", "radialGradient", "stop", "clipPath", "mask", "title", "desc", "use", "symbol",
    ];

    [Fact]
    public void ASimpleSvgKeepsItsAllowedElementsAndAttributes()
    {
        XElement root = Clean($"<svg {Ns} viewBox=\"0 0 10 10\" width=\"10\"><rect width=\"4\" fill=\"#FF0000\"/><path d=\"M0 0L5 5\"/></svg>");

        Assert.Equal("0 0 10 10", (string?)root.Attribute("viewBox"));
        Assert.Equal("10", (string?)root.Attribute("width"));
        Assert.Equal("#FF0000", (string?)root.Element(Svg + "rect")?.Attribute("fill"));
        Assert.Equal("M0 0L5 5", (string?)root.Element(Svg + "path")?.Attribute("d"));
    }

    [Fact]
    public void TheOutputIsUtf8WithoutAByteOrderMark()
    {
        byte[]? output = SanitizeBytes(Wrap("<rect/>"));

        Assert.NotNull(output);
        Assert.False(output.Length >= 3 && output[0] == 0xEF && output[1] == 0xBB && output[2] == 0xBF);
    }

    [Fact]
    public void AnInputWithAByteOrderMarkIsAccepted()
    {
        byte[] input = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Wrap("<rect/>"))];

        byte[]? output = _sanitizer.Sanitize(input);

        Assert.NotNull(output);
        Assert.False(output[0] == 0xEF);
    }

    [Theory]
    [MemberData(nameof(AllowedElementNames))]
    public void EveryAllowedElementSurvives(string name)
    {
        XElement root = Clean(Wrap($"<{name}/>"));

        Assert.NotNull(root.Element(Svg + name));
    }

    [Fact]
    public void TheRootMustBeAnSvgInTheSvgNamespace()
    {
        Assert.Null(SanitizeBytes("<svg><rect/></svg>"));
        Assert.Null(SanitizeBytes("<html xmlns=\"http://www.w3.org/1999/xhtml\"/>"));
        Assert.Null(SanitizeBytes($"<g {Ns}/>"));
    }

    [Fact]
    public void NotXmlReturnsNull()
    {
        Assert.Null(SanitizeBytes("hello"));
        Assert.Null(SanitizeBytes(string.Empty));
    }

    [Fact]
    public void TruncatedXmlReturnsNull()
    {
        Assert.Null(SanitizeBytes($"<svg {Ns}><rect"));
        Assert.Null(SanitizeBytes($"<svg {Ns}><g>"));
    }

    [Fact]
    public void ADoctypeWithAnExternalEntityIsRefused()
    {
        const string xxe = "<!DOCTYPE svg [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><svg xmlns=\"http://www.w3.org/2000/svg\"><text>&x;</text></svg>";

        Assert.Null(SanitizeBytes(xxe));
    }

    [Fact]
    public void AnyDoctypeIsRefusedEvenWithoutEntities()
    {
        Assert.Null(SanitizeBytes($"<!DOCTYPE svg><svg {Ns}/>"));
    }

    [Fact]
    public void ADocumentOverTwoMillionCharactersReturnsNull()
    {
        string huge = Wrap("<text>" + new string('a', 2_000_001) + "</text>");

        Assert.Null(SanitizeBytes(huge));
    }

    [Fact]
    public void ADocumentJustUnderTheLimitIsAccepted()
    {
        string large = Wrap("<text>" + new string('a', 1_900_000) + "</text>");

        Assert.NotNull(SanitizeBytes(large));
    }

    [Fact]
    public void AScriptElementIsRemovedWithItsContent()
    {
        XElement root = Clean(Wrap("<script>alert(1)</script><rect/>"));

        Assert.Null(root.Element(Svg + "script"));
        Assert.NotNull(root.Element(Svg + "rect"));
        Assert.DoesNotContain("alert", root.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("foreignObject")]
    [InlineData("image")]
    [InlineData("a")]
    [InlineData("style")]
    [InlineData("animate")]
    [InlineData("set")]
    [InlineData("animateTransform")]
    [InlineData("filter")]
    [InlineData("iframe")]
    public void DisallowedElementsAreRemoved(string name)
    {
        XElement root = Clean(Wrap($"<{name}/><rect/>"));

        Assert.Null(root.Element(Svg + name));
        Assert.NotNull(root.Element(Svg + "rect"));
    }

    [Fact]
    public void AnElementInAnUnknownNamespaceIsRemoved()
    {
        XElement root = Clean(Wrap("<x:foo xmlns:x=\"urn:evil\"/><rect/>"));

        Assert.Single(root.Elements());
        Assert.NotNull(root.Element(Svg + "rect"));
    }

    [Fact]
    public void AnSvgNamedElementInAForeignNamespaceIsRemoved()
    {
        XElement root = Clean(Wrap("<rect xmlns=\"urn:evil\"/><circle/>"));

        Assert.Single(root.Elements());
        Assert.NotNull(root.Element(Svg + "circle"));
    }

    [Fact]
    public void ADisallowedElementNestedInAnAllowedOneIsRemovedWhileSiblingsStay()
    {
        XElement root = Clean(Wrap("<g><script>x</script><rect/><image/><circle/></g>"));

        XElement group = Assert.Single(root.Elements(Svg + "g"));
        Assert.Equal(["rect", "circle"], group.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void ADisallowedElementKeepsItsChildrenOutToo()
    {
        XElement root = Clean(Wrap("<a><rect/></a>"));

        Assert.Empty(root.Elements());
    }

    [Theory]
    [InlineData("onload")]
    [InlineData("onclick")]
    [InlineData("onmouseover")]
    [InlineData("ONLOAD")]
    [InlineData("OnClick")]
    public void EventHandlerAttributesAreRemoved(string attribute)
    {
        XElement root = Clean($"<svg {Ns} {attribute}=\"alert(1)\"><rect {attribute}=\"alert(1)\" width=\"3\"/></svg>");

        Assert.Null(root.Attribute(attribute));
        XElement rect = Assert.Single(root.Elements());
        Assert.Null(rect.Attribute(attribute));
        Assert.Equal("3", (string?)rect.Attribute("width"));
    }

    [Fact]
    public void TheStyleAttributeIsRemoved()
    {
        XElement root = Clean(Wrap("<rect style=\"fill:red\" STYLE=\"x\" width=\"3\"/>"));

        XElement rect = Assert.Single(root.Elements());
        Assert.Null(rect.Attribute("style"));
        Assert.Null(rect.Attribute("STYLE"));
        Assert.NotNull(rect.Attribute("width"));
    }

    [Theory]
    [InlineData("#a")]
    [InlineData("  #a")]
    public void AFragmentHrefIsKept(string href)
    {
        XElement root = Clean(Wrap($"<use href=\"{href}\"/>"));

        Assert.Equal(href, (string?)Assert.Single(root.Elements()).Attribute("href"));
    }

    [Fact]
    public void AFragmentXlinkHrefIsKept()
    {
        XElement root = Clean($"<svg {Ns} xmlns:xlink=\"http://www.w3.org/1999/xlink\"><use xlink:href=\"#a\"/></svg>");

        XElement use = Assert.Single(root.Elements());
        Assert.Equal("#a", (string?)use.Attribute(XNamespace.Get("http://www.w3.org/1999/xlink") + "href"));
    }

    [Theory]
    [InlineData("http://evil.example/x.svg#a")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,AAAA")]
    [InlineData("a.svg#x")]
    [InlineData("")]
    public void AnExternalHrefIsRemoved(string href)
    {
        XElement root = Clean(Wrap($"<use href=\"{href}\"/>"));

        Assert.Null(Assert.Single(root.Elements()).Attribute("href"));
    }

    [Fact]
    public void AnExternalXlinkHrefIsRemoved()
    {
        XElement root = Clean($"<svg {Ns} xmlns:xlink=\"http://www.w3.org/1999/xlink\"><use xlink:href=\"http://evil.example/\"/></svg>");

        Assert.Empty(Assert.Single(root.Elements()).Attributes());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("DATA:text/html,x")]
    [InlineData("x javascript: y")]
    public void AValueContainingAScriptOrDataSchemeIsRemoved(string value)
    {
        XElement root = Clean(Wrap($"<rect fill=\"{value}\" width=\"3\"/>"));

        XElement rect = Assert.Single(root.Elements());
        Assert.Null(rect.Attribute("fill"));
        Assert.NotNull(rect.Attribute("width"));
    }

    [Theory]
    [InlineData("url(#g)")]
    [InlineData("url( #g)")]
    [InlineData("URL(#g)")]
    public void AFragmentUrlIsKept(string value)
    {
        XElement root = Clean(Wrap($"<rect fill=\"{value}\"/>"));

        Assert.Equal(value, (string?)Assert.Single(root.Elements()).Attribute("fill"));
    }

    [Theory]
    [InlineData("url(http://x)")]
    [InlineData("url( 'x')")]
    [InlineData("url('x')")]
    [InlineData("url(x.png)")]
    [InlineData("URL(//evil/x)")]
    public void ANonFragmentUrlIsRemoved(string value)
    {
        XElement root = Clean(Wrap($"<rect fill=\"{value}\"/>"));

        Assert.Null(Assert.Single(root.Elements()).Attribute("fill"));
    }

    [Fact]
    public void AnAttributeInAForeignNamespaceIsRemovedButXmlSpaceIsKept()
    {
        XElement root = Clean(Wrap("<text xmlns:foo=\"urn:foo\" foo:bar=\"1\" xml:space=\"preserve\">hi</text>"));

        XElement text = Assert.Single(root.Elements());
        Assert.Null(text.Attribute(XNamespace.Get("urn:foo") + "bar"));
        Assert.Equal("preserve", (string?)text.Attribute(XNamespace.Xml + "space"));
    }

    [Fact]
    public void NamespaceDeclarationsAreKept()
    {
        XElement root = Clean($"<svg {Ns} xmlns:xlink=\"http://www.w3.org/1999/xlink\"><rect/></svg>");

        Assert.Contains(root.Attributes(), a => a.IsNamespaceDeclaration && a.Name.LocalName == "xlink");
        Assert.Equal(Svg, root.Name.Namespace);
    }

    [Fact]
    public void CommentsAndProcessingInstructionsAreDropped()
    {
        XElement root = Clean($"<svg {Ns}><!-- secret --><?pi payload?><rect/></svg>");

        Assert.Empty(root.Nodes().OfType<XComment>());
        Assert.Empty(root.Nodes().OfType<XProcessingInstruction>());
        Assert.Single(root.Elements());
    }

    [Fact]
    public void TextContentOfTextAndTspanIsPreserved()
    {
        XElement root = Clean(Wrap("<text>Hello <tspan>مرحبا</tspan></text>"));

        XElement text = Assert.Single(root.Elements());
        Assert.Equal("Hello مرحبا", text.Value);
        Assert.Equal("مرحبا", text.Element(Svg + "tspan")?.Value);
    }

    [Fact]
    public void TheOutputIsWellFormedXml()
    {
        byte[]? output = SanitizeBytes(Wrap("<script/><rect onclick=\"x\"/>"));

        Assert.NotNull(output);
        XDocument reparsed = XDocument.Load(XmlReader.Create(new MemoryStream(output), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }));
        Assert.NotNull(reparsed.Root);
    }

    [Fact]
    public void SanitisingTheOutputAgainChangesNothing()
    {
        byte[]? once = SanitizeBytes(Wrap("<script/><g onclick=\"x\"><rect style=\"a\" fill=\"url(#g)\"/><text>Hi</text></g>"));

        Assert.NotNull(once);
        Assert.Equal(once, _sanitizer.Sanitize(once));
    }

    [Fact]
    public void ARealisticLogoSurvivesIntact()
    {
        const string logo =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 160 40\" width=\"160\" height=\"40\">" +
            "<rect x=\"2\" y=\"6\" width=\"28\" height=\"28\" rx=\"7\" fill=\"#0E6655\"/>" +
            "<path d=\"M10 26 L16 12 L22 26 Z\" fill=\"#FFFFFF\"/>" +
            "<text x=\"40\" y=\"28\" font-family=\"sans-serif\" font-size=\"22\" font-weight=\"700\" fill=\"#0E6655\">DEMO</text></svg>";

        XElement root = Clean(logo);

        Assert.Equal(["rect", "path", "text"], root.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("DEMO", root.Element(Svg + "text")?.Value);
        Assert.Equal("sans-serif", (string?)root.Element(Svg + "text")?.Attribute("font-family"));
    }

    [Fact]
    public void SanitizeRejectsNull()
    {
        Func<byte[], byte[]?> sanitize = _sanitizer.Sanitize;

        NullArguments.AssertThrowsArgumentNull(sanitize, new object?[] { null });
    }
}
