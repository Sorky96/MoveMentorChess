using System.Globalization;
using System.Text.Json;

namespace MoveMentorChess.Persistence;

internal static class SqliteAnalysisDataConverters
{
    private const int NoMoveTimeMs = -1;

    public static readonly JsonSerializerOptions JsonOptions = new();

    public static int NormalizeMoveTime(int? moveTimeMs) => moveTimeMs ?? NoMoveTimeMs;

    public static int? ReadMoveTime(int rawMoveTime) => rawMoveTime == NoMoveTimeMs ? null : rawMoveTime;

    public static string FormatUtc(DateTime value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static DateTime ParseUtc(string? value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
    }

    public static DateTime? ParseNullableUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ParseUtc(value);
    }

    public static AdviceFeedbackKind? ParseNullableFeedbackKind(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out AdviceFeedbackKind parsed)
            ? parsed
            : null;
    }

    public static IReadOnlyList<string> DeserializeEvidence(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(payload, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static double? ParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : null;
    }

    public static string? FormatNullableDouble(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.####", CultureInfo.InvariantCulture)
            : null;
    }

    public static string SerializeEvidence(IReadOnlyList<string>? evidence)
    {
        return JsonSerializer.Serialize(evidence ?? [], JsonOptions);
    }
}
