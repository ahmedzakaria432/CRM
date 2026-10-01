namespace Crm.Modules.Organisation.Application.Theming;

/// <summary>Strips everything executable from an uploaded SVG (Story 58, S2). Implemented in Infrastructure.</summary>
public interface ISvgSanitizer
{
    /// <summary>The sanitised document, or null when the input is not a well-formed SVG document.</summary>
    byte[]? Sanitize(byte[] svg);
}
