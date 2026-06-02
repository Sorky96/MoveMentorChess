namespace MoveMentorChess.Presentation.Models;

public enum ProfileTrendChartKind
{
    Line,
    Bars
}

public sealed record ProfileTrendChartPoint(string Label, double? Value);

public sealed record ProfileTrendChartSeries(
    string Name,
    string StrokeHex,
    IReadOnlyList<ProfileTrendChartPoint> Points,
    ProfileTrendChartKind Kind = ProfileTrendChartKind.Line)
{
    private string strokeHex = ValidateStrokeHex(StrokeHex, nameof(StrokeHex));

    public string StrokeHex
    {
        get => strokeHex;
        init => strokeHex = ValidateStrokeHex(value, nameof(StrokeHex));
    }

    private static string ValidateStrokeHex(string? value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Chart stroke color cannot be blank.", parameterName);
        }

        ReadOnlySpan<char> hex = trimmed.StartsWith('#') ? trimmed.AsSpan(1) : trimmed.AsSpan();
        if (hex.Length is not (6 or 8))
        {
            throw new ArgumentException("Chart stroke color must be #RRGGBB, RRGGBB, #AARRGGBB, or AARRGGBB.", parameterName);
        }

        foreach (char c in hex)
        {
            bool isHexDigit = c is >= '0' and <= '9'
                || c is >= 'A' and <= 'F'
                || c is >= 'a' and <= 'f';
            if (!isHexDigit)
            {
                throw new ArgumentException("Chart stroke color must contain only hex digits.", parameterName);
            }
        }

        return trimmed;
    }
}
