namespace Crm.BuildingBlocks.Presentation.ErrorHandling;

/// <summary>
/// ValidationBehavior joins failures as "Field: message | Field: message" into Error.Description (Error
/// carries no field map). This splits them back into the RFC 9457 <c>errors</c> dictionary. A message that
/// itself contains " | " or ":" is misattributed - the status and type stay correct.
/// </summary>
public static class ValidationErrorParser
{
    public static Dictionary<string, string[]> Parse(string description) =>
        description
            .Split(" | ", StringSplitOptions.RemoveEmptyEntries)
            .Select(entry =>
            {
                int separator = entry.IndexOf(':', StringComparison.Ordinal);
                return separator < 0
                    ? (Field: entry, Message: entry)
                    : (Field: entry[..separator].Trim(), Message: entry[(separator + 1)..].Trim());
            })
            .GroupBy(x => x.Field)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());
}
