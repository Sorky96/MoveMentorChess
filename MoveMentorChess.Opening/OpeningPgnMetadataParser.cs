using System.Text.RegularExpressions;

namespace MoveMentorChess.Opening;

public static partial class OpeningPgnMetadataParser
{
    [GeneratedRegex(@"^\[(?<key>[A-Za-z0-9_]+)\s+""(?<value>.*)""\]\s*$", RegexOptions.Multiline)]
    private static partial Regex GetHeaderRegex();

    public static OpeningGameMetadata Parse(string pgn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pgn);

        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in GetHeaderRegex().Matches(pgn))
        {
            headers[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        string eco = GetValue(headers, "ECO");
        string opening = GetValue(headers, "Opening");
        string variation = GetValue(headers, "Variation");

        return new OpeningGameMetadata(eco, opening, variation);
    }

    private static string GetValue(Dictionary<string, string> headers, string key)
    {
        return headers.TryGetValue(key, out string? value) ? value : string.Empty;
    }
}
