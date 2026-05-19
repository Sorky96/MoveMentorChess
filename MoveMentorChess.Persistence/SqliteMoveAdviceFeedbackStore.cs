using System.Globalization;
using System.Text.Json;

namespace MoveMentorChess.Persistence;

internal static class SqliteMoveAdviceFeedbackStore
{
    private const int SqliteRow = SqliteResult.Row;
    private const int NoMoveTimeMs = -1;

    private static readonly JsonSerializerOptions JsonOptions = new();

    public static IReadOnlyList<MoveAdviceFeedback> ListMoveAdviceFeedback(
        SqliteDatabase database,
        string? filterText = null,
        int limit = 5000)
    {
        string normalizedFilter = filterText?.Trim().ToLowerInvariant() ?? string.Empty;
        int safeLimit = Math.Clamp(limit, 1, 20000);
        List<MoveAdviceFeedback> items = [];

        using SqliteStatement statement = database.Prepare($"""
            SELECT
                feedback_id,
                timestamp_utc,
                game_fingerprint,
                analyzed_side,
                depth,
                multi_pv,
                move_time_ms,
                ply,
                move_number,
                played_san,
                played_uci,
                fen_before,
                fen_after,
                eval_before_cp,
                eval_after_cp,
                best_move_uci,
                original_label,
                original_confidence,
                original_evidence_json,
                quality,
                centipawn_loss,
                feedback_kind,
                corrected_label,
                comment,
                source
            FROM move_advice_feedbacks
            {(string.IsNullOrWhiteSpace(normalizedFilter)
                ? string.Empty
                : "WHERE lower(coalesce(original_label, '')) LIKE ?1 OR lower(coalesce(corrected_label, '')) LIKE ?1 OR lower(coalesce(comment, '')) LIKE ?1 OR lower(played_san) LIKE ?1 OR lower(played_uci) LIKE ?1")}
            ORDER BY timestamp_utc DESC, feedback_id DESC
            LIMIT {safeLimit};
            """);

        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            statement.BindText(1, $"%{normalizedFilter}%");
        }

        while (statement.Step() == SqliteRow)
        {
            items.Add(ReadMoveAdviceFeedback(statement));
        }

        return items;
    }

    public static void SaveMoveAdviceFeedback(SqliteDatabase database, MoveAdviceFeedback feedback)
    {
        using SqliteStatement statement = database.Prepare("""
            INSERT INTO move_advice_feedbacks (
                feedback_id,
                timestamp_utc,
                game_fingerprint,
                analyzed_side,
                depth,
                multi_pv,
                move_time_ms,
                ply,
                move_number,
                played_san,
                played_uci,
                fen_before,
                fen_after,
                eval_before_cp,
                eval_after_cp,
                best_move_uci,
                original_label,
                original_confidence,
                original_evidence_json,
                quality,
                centipawn_loss,
                feedback_kind,
                corrected_label,
                comment,
                source)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15, ?16, ?17, ?18, ?19, ?20, ?21, ?22, ?23, ?24, ?25);
            """);

        statement.BindText(1, string.IsNullOrWhiteSpace(feedback.FeedbackId) ? Guid.NewGuid().ToString("N") : feedback.FeedbackId);
        statement.BindText(2, feedback.TimestampUtc.ToUniversalTime().ToString("O"));
        statement.BindText(3, feedback.GameFingerprint);
        statement.BindInt(4, (int)feedback.AnalyzedSide);
        statement.BindInt(5, feedback.Depth);
        statement.BindInt(6, feedback.MultiPv);
        statement.BindInt(7, NormalizeMoveTime(feedback.MoveTimeMs));
        statement.BindInt(8, feedback.Ply);
        statement.BindInt(9, feedback.MoveNumber);
        statement.BindText(10, feedback.PlayedSan);
        statement.BindText(11, feedback.PlayedUci);
        statement.BindText(12, feedback.FenBefore);
        statement.BindText(13, feedback.FenAfter);
        statement.BindNullableInt(14, feedback.EvalBeforeCp);
        statement.BindNullableInt(15, feedback.EvalAfterCp);
        statement.BindNullableText(16, feedback.BestMoveUci);
        statement.BindNullableText(17, feedback.OriginalLabel);
        statement.BindNullableText(18, FormatNullableDouble(feedback.OriginalConfidence));
        statement.BindText(19, SerializeEvidence(feedback.OriginalEvidence));
        statement.BindInt(20, (int)feedback.Quality);
        statement.BindNullableInt(21, feedback.CentipawnLoss);
        statement.BindText(22, feedback.FeedbackKind.ToString());
        statement.BindNullableText(23, feedback.CorrectedLabel);
        statement.BindNullableText(24, feedback.Comment);
        statement.BindText(25, feedback.Source);
        statement.StepUntilDone();
    }

    private static MoveAdviceFeedback ReadMoveAdviceFeedback(SqliteStatement statement)
    {
        return new MoveAdviceFeedback(
            statement.GetText(0) ?? string.Empty,
            ParseUtc(statement.GetText(1)),
            statement.GetText(2) ?? string.Empty,
            (PlayerSide)statement.GetInt(3),
            statement.GetInt(4),
            statement.GetInt(5),
            ReadMoveTime(statement.GetInt(6)),
            statement.GetInt(7),
            statement.GetInt(8),
            statement.GetText(9) ?? string.Empty,
            statement.GetText(10) ?? string.Empty,
            statement.GetText(11) ?? string.Empty,
            statement.GetText(12) ?? string.Empty,
            statement.GetNullableInt(13),
            statement.GetNullableInt(14),
            statement.GetText(15),
            statement.GetText(16),
            ParseNullableDouble(statement.GetText(17)),
            DeserializeEvidence(statement.GetText(18)),
            (MoveQualityBucket)statement.GetInt(19),
            statement.GetNullableInt(20),
            ParseNullableFeedbackKind(statement.GetText(21)) ?? AdviceFeedbackKind.NotUseful,
            statement.GetText(22),
            statement.GetText(23),
            statement.GetText(24) ?? string.Empty);
    }

    private static int NormalizeMoveTime(int? moveTimeMs) => moveTimeMs ?? NoMoveTimeMs;

    private static int? ReadMoveTime(int rawMoveTime) => rawMoveTime == NoMoveTimeMs ? null : rawMoveTime;

    private static DateTime ParseUtc(string? value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private static AdviceFeedbackKind? ParseNullableFeedbackKind(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out AdviceFeedbackKind parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyList<string> DeserializeEvidence(string? payload)
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

    private static double? ParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : null;
    }

    private static string? FormatNullableDouble(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.####", CultureInfo.InvariantCulture)
            : null;
    }

    private static string SerializeEvidence(IReadOnlyList<string>? evidence)
    {
        return JsonSerializer.Serialize(evidence ?? [], JsonOptions);
    }
}
