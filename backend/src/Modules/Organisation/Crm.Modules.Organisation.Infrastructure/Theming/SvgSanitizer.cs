using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Crm.Modules.Organisation.Application.Theming;

namespace Crm.Modules.Organisation.Infrastructure.Theming;

/// <summary>Allow-list SVG sanitiser (Story 58, S2): parses without DTDs or external resolution, keeps only known
/// graphic elements, drops every event handler, style, script-capable or external reference. The stored bytes are
/// the sanitised output.</summary>
public sealed partial class SvgSanitizer : ISvgSanitizer
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    private static readonly FrozenSet<string> AllowedElements = new[]
    {
        "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text", "tspan", "defs",
        "linearGradient", "radialGradient", "stop", "clipPath", "mask", "title", "desc", "use", "symbol",
    }.ToFrozenSet(StringComparer.Ordinal);

    public byte[]? Sanitize(byte[] svg)
    {
        ArgumentNullException.ThrowIfNull(svg);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 2_000_000,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        XDocument document;
        try
        {
            using var input = new MemoryStream(svg);
            using var reader = XmlReader.Create(input, settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }

        if (document.Root is not { } root || root.Name != Svg + "svg")
        {
            return null;
        }

        Clean(root);

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            new XDocument(root).Save(writer);
        }

        return output.ToArray();
    }

    private static void Clean(XElement element)
    {
        foreach (XElement child in element.Elements().ToList())
        {
            if (child.Name.Namespace != Svg || !AllowedElements.Contains(child.Name.LocalName))
            {
                child.Remove();
                continue;
            }

            Clean(child);
        }

        foreach (XAttribute attribute in element.Attributes().ToList())
        {
            if (!IsAllowed(attribute))
            {
                attribute.Remove();
            }
        }
    }

    private static bool IsAllowed(XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration)
        {
            return true;
        }

        string local = attribute.Name.LocalName;
        if (local.StartsWith("on", StringComparison.OrdinalIgnoreCase) || local.Equals("style", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        XNamespace ns = attribute.Name.Namespace;
        if (local.Equals("href", StringComparison.OrdinalIgnoreCase) && (ns == XNamespace.None || ns == XLink))
        {
            return attribute.Value.Trim().StartsWith('#');
        }

        if (ns != XNamespace.None && ns != XNamespace.Xml)
        {
            return false;
        }

        string value = attribute.Value;
        if (value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) || value.Contains("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !(value.Contains("url(", StringComparison.OrdinalIgnoreCase) && NonFragmentUrl().IsMatch(value));
    }

    [GeneratedRegex(@"url\(\s*[^#\s)]", RegexOptions.IgnoreCase)]
    private static partial Regex NonFragmentUrl();
}
