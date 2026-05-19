using System.Globalization;
using System.Text.Json;

namespace MoveMentorChess.Persistence;

internal static class SqliteOpeningTrainingDataConverters
{
    public static readonly JsonSerializerOptions JsonOptions = new();

    public static string NormalizePlayerKey(string? playerKey)
        => string.IsNullOrWhiteSpace(playerKey) ? string.Empty : playerKey.Trim().ToLowerInvariant();

    public static string? NormalizeNullablePlayerKey(string? playerKey)
    {
        return string.IsNullOrWhiteSpace(playerKey)
            ? null
            : playerKey.Trim().ToLowerInvariant();
    }

    public static string FormatUtc(DateTime value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static string? FormatNullableUtc(DateTime? value)
        => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

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

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime parsed)
            ? parsed
            : null;
    }

    public static double ParseDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : 0;
    }

    public static string FormatDouble(double value)
        => value.ToString(CultureInfo.InvariantCulture);
}
