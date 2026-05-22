namespace MoveMentorChess.Training;

public sealed class OpeningTrainingMoveEvaluator
{
    public OpeningTrainingAttemptResult EvaluateMove(OpeningTrainingPosition position, string submittedMoveText)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (position.AnswerKind != OpeningTrainingAnswerKind.Move)
        {
            throw new ArgumentException("Move evaluation is available only for move-answer positions.", nameof(position));
        }

        List<OpeningTrainingMoveOption> preferredReferences = GetPreferredReferences(position);
        List<OpeningTrainingMoveOption> playableReferences = GetPlayableReferences(position, preferredReferences);
        IReadOnlyList<OpeningTrainingMoveOption> expectedMoves = preferredReferences
            .Concat(playableReferences)
            .DistinctBy(option => BuildMoveKey(option.Uci, option.DisplayText))
            .ToList();

        if (!TryResolveMoveInput(position.Fen, submittedMoveText, out AppliedMoveInfo? resolvedMove, out string? error)
            || resolvedMove is null)
        {
            string invalidMoveExplanation = string.IsNullOrWhiteSpace(error)
                ? "The submitted move could not be matched to a legal move in this position."
                : error;

            return new OpeningTrainingAttemptResult(
                position.PositionId,
                position.Mode,
                position.SourceKind,
                OpeningTrainingAttemptStatus.Normal,
                submittedMoveText,
                null,
                null,
                expectedMoves,
                OpeningTrainingScore.Wrong,
                invalidMoveExplanation,
                [],
                preferredReferences,
                playableReferences);
        }

        OpeningPositionKey resolvedPositionKey = OpeningPositionKeyBuilder.BuildKey(resolvedMove.FenAfter);
        List<OpeningTrainingMoveOption> matchingReferences = position.CandidateMoves
            .Where(option => MovesMatch(option, resolvedMove))
            .ToList();
        List<OpeningTrainingMoveOption> transposedReferences = position.CandidateMoves
            .Where(option => option.ToPositionKey.HasValue
                && option.ToPositionKey.Value.Equals(resolvedPositionKey))
            .ToList();
        if (matchingReferences.Count == 0 && transposedReferences.Count > 0)
        {
            matchingReferences = transposedReferences;
        }

        bool matchedPreferred = preferredReferences.Any(reference => MovesMatch(reference, resolvedMove))
            || matchingReferences.Any(match =>
                preferredReferences.Any(reference => MoveOptionsMatch(reference, match)));
        bool matchedPlayable = playableReferences.Any(reference => MovesMatch(reference, resolvedMove))
            || matchingReferences.Any(match =>
                playableReferences.Any(reference => MoveOptionsMatch(reference, match)));
        bool transposedToKnownPosition = transposedReferences.Count > 0 && !position.CandidateMoves.Any(option => MovesMatch(option, resolvedMove));
        OpeningTrainingScore score = DetermineScore(position.Strictness, matchedPreferred, matchedPlayable, transposedToKnownPosition);
        OpeningTrainingAttemptStatus status = transposedToKnownPosition
            ? OpeningTrainingAttemptStatus.TransposedToKnownPosition
            : OpeningTrainingAttemptStatus.Normal;
        OpeningMoveIdea? whyThisMove = matchingReferences
            .Select(option => option.Idea)
            .FirstOrDefault(idea => idea is not null)
            ?? preferredReferences.Select(option => option.Idea).FirstOrDefault(idea => idea is not null);
        string explanation = BuildAttemptExplanation(position, score, matchingReferences, preferredReferences, playableReferences);
        if (status == OpeningTrainingAttemptStatus.TransposedToKnownPosition)
        {
            explanation = $"{explanation} The move reached a known theory position by transposition.";
        }

        return new OpeningTrainingAttemptResult(
            position.PositionId,
            position.Mode,
            position.SourceKind,
            status,
            submittedMoveText,
            resolvedMove.San,
            resolvedMove.Uci,
            expectedMoves,
            score,
            explanation,
            matchingReferences,
            preferredReferences,
            playableReferences,
            new OpeningPositionIdentity(
                resolvedPositionKey,
                resolvedMove.FenAfter,
                OpeningPositionKeyBuilder.NormalizeFen(resolvedMove.FenAfter),
                position.Ply + 1,
                position.MoveNumber,
                Opponent(position.SideToMove),
                status == OpeningTrainingAttemptStatus.TransposedToKnownPosition),
            whyThisMove);
    }

    public static string BuildBetterMoveSummary(
        OpeningTrainingPosition position,
        IReadOnlyList<OpeningTrainingMoveOption> preferredReferences)
    {
        string? preferredTheoryMove = GetPreferredTheoryMoveDisplay(preferredReferences);
        if (!string.IsNullOrWhiteSpace(preferredTheoryMove))
        {
            return $"Better move: {preferredTheoryMove}.";
        }

        if (!string.IsNullOrWhiteSpace(position.BetterMove))
        {
            return $"Better move: {position.BetterMove}.";
        }

        return "Better move: no saved repair move was available.";
    }

    public static string BuildWhyBetterSummary(
        OpeningTrainingPosition position,
        IReadOnlyList<OpeningTrainingMoveOption> preferredReferences,
        IReadOnlyList<OpeningTrainingMoveOption> playableReferences)
    {
        if (!string.IsNullOrWhiteSpace(position.BetterMoveReason))
        {
            return $"Why: {EnsureSentence(position.BetterMoveReason!)}";
        }

        string? note = preferredReferences
            .Concat(playableReferences)
            .Select(option => option.Note)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return string.IsNullOrWhiteSpace(note)
            ? "Why: it was the healthier opening choice in this position."
            : $"Why: {EnsureSentence(note!)}";
    }

    private static List<OpeningTrainingMoveOption> GetPreferredReferences(OpeningTrainingPosition position)
    {
        if (position.Mode != OpeningTrainingMode.BranchAwareness)
        {
            return position.CandidateMoves
                .Where(option => option.IsPreferred)
                .ToList();
        }

        OpeningTrainingBranch? primaryBranch = position.Branches?
            .OrderByDescending(branch => branch.Frequency)
            .ThenBy(branch => branch.OpponentMove, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (primaryBranch is null)
        {
            return [];
        }

        OpeningTrainingMoveOption? primaryOption = position.CandidateMoves.FirstOrDefault(option =>
            option.Role == OpeningTrainingMoveRole.Alternative
            && MoveOptionMatchesTextAndUci(option, primaryBranch.OpponentMove, primaryBranch.OpponentMoveUci));

        return primaryOption is null ? [] : [primaryOption];
    }

    private static List<OpeningTrainingMoveOption> GetPlayableReferences(
        OpeningTrainingPosition position,
        List<OpeningTrainingMoveOption> preferredReferences)
    {
        IEnumerable<OpeningTrainingMoveOption> playable = position.Mode switch
        {
            OpeningTrainingMode.MistakeRepair => position.CandidateMoves
                .Where(option => !option.IsPreferred && option.Role == OpeningTrainingMoveRole.Repair),
            OpeningTrainingMode.BranchAwareness => position.CandidateMoves
                .Where(option => option.Role == OpeningTrainingMoveRole.Alternative),
            _ => position.CandidateMoves.Where(option => !option.IsPreferred)
        };

        return playable
            .Where(option => !preferredReferences.Any(reference => MoveOptionsMatch(reference, option)))
            .ToList();
    }

    private static string BuildAttemptExplanation(
        OpeningTrainingPosition position,
        OpeningTrainingScore score,
        IReadOnlyList<OpeningTrainingMoveOption> matchingReferences,
        IReadOnlyList<OpeningTrainingMoveOption> preferredReferences,
        IReadOnlyList<OpeningTrainingMoveOption> playableReferences)
    {
        return position.Mode switch
        {
            OpeningTrainingMode.MistakeRepair => BuildMistakeRepairAttemptExplanation(position, score, preferredReferences, playableReferences),
            OpeningTrainingMode.BranchAwareness => BuildBranchAwarenessAttemptExplanation(score, matchingReferences, preferredReferences, playableReferences),
            _ => BuildLineRecallAttemptExplanation(score, matchingReferences, preferredReferences)
        };
    }

    private static string BuildLineRecallAttemptExplanation(
        OpeningTrainingScore score,
        IReadOnlyList<OpeningTrainingMoveOption> matchingReferences,
        IReadOnlyList<OpeningTrainingMoveOption> preferredReferences)
    {
        return score switch
        {
            OpeningTrainingScore.Correct => $"Accepted as correct. The move matches a preferred imported-theory reference: {FormatMatchedReferences(matchingReferences.Where(option => option.IsPreferred).ToList())}.",
            OpeningTrainingScore.Playable => $"Accepted as playable. The move appears in imported opening theory, but it is not the strongest preferred continuation here: {FormatMatchedReferences(matchingReferences)}.",
            _ => preferredReferences.Count == 0
                ? "Marked as wrong because the move does not match any imported-theory move for this position."
                : $"Marked as wrong because the move does not match the preferred imported-theory references: {FormatMatchedReferences(preferredReferences)}."
        };
    }

    private static string BuildMistakeRepairAttemptExplanation(
        OpeningTrainingPosition position,
        OpeningTrainingScore score,
        IReadOnlyList<OpeningTrainingMoveOption> preferredReferences,
        IReadOnlyList<OpeningTrainingMoveOption> playableReferences)
    {
        string betterMoveSummary = BuildBetterMoveSummary(position, preferredReferences);
        string whyBetter = BuildWhyBetterSummary(position, preferredReferences, playableReferences);

        return score switch
        {
            OpeningTrainingScore.Correct => $"Correct repair. {betterMoveSummary} {whyBetter}",
            OpeningTrainingScore.Playable => $"Playable repair. {betterMoveSummary} {whyBetter}",
            _ => $"Wrong repair. {betterMoveSummary} {whyBetter}"
        };
    }

    private static string BuildBranchAwarenessAttemptExplanation(
        OpeningTrainingScore score,
        IReadOnlyList<OpeningTrainingMoveOption> matchingReferences,
        IReadOnlyList<OpeningTrainingMoveOption> preferredReferences,
        IReadOnlyList<OpeningTrainingMoveOption> playableReferences)
    {
        return score switch
        {
            OpeningTrainingScore.Correct => $"Correct branch. This is the highest-priority imported-theory opponent reply: {FormatMatchedReferences(preferredReferences)}.",
            OpeningTrainingScore.Playable => $"Playable branch. This opponent reply appears in imported theory: {FormatMatchedReferences(matchingReferences)}. Primary branch: {FormatMatchedReferences(preferredReferences)}.",
            _ => preferredReferences.Count == 0 && playableReferences.Count == 0
                ? "Marked as wrong because no imported-theory opponent branches are available for this position."
                : $"Marked as wrong because the move does not match the imported-theory opponent branches: {FormatMatchedReferences(preferredReferences.Concat(playableReferences).ToList())}."
        };
    }

    private static bool TryResolveMoveInput(string fen, string submittedMoveText, out AppliedMoveInfo? appliedMove, out string? error)
    {
        appliedMove = null;
        error = null;

        if (string.IsNullOrWhiteSpace(submittedMoveText))
        {
            error = "Move cannot be empty.";
            return false;
        }

        ChessGame game = new();
        if (!game.TryLoadFen(fen, out error))
        {
            return false;
        }

        if (game.TryApplyUci(submittedMoveText.Trim(), out appliedMove, out error) && appliedMove is not null)
        {
            return true;
        }

        ChessGame sanGame = new();
        if (!sanGame.TryLoadFen(fen, out error))
        {
            return false;
        }

        try
        {
            appliedMove = sanGame.ApplySanWithResult(submittedMoveText.Trim());
            error = null;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            appliedMove = null;
            return false;
        }
    }

    private static bool MovesMatch(OpeningTrainingMoveOption option, AppliedMoveInfo resolvedMove)
    {
        if (!string.IsNullOrWhiteSpace(option.Uci)
            && string.Equals(option.Uci, resolvedMove.Uci, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            SanNotation.NormalizeSan(option.DisplayText),
            SanNotation.NormalizeSan(resolvedMove.San),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool MoveOptionsMatch(OpeningTrainingMoveOption left, OpeningTrainingMoveOption right)
    {
        if (!string.IsNullOrWhiteSpace(left.Uci)
            && !string.IsNullOrWhiteSpace(right.Uci)
            && string.Equals(left.Uci, right.Uci, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            SanNotation.NormalizeSan(left.DisplayText),
            SanNotation.NormalizeSan(right.DisplayText),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool MoveOptionMatchesTextAndUci(OpeningTrainingMoveOption option, string displayText, string? uci)
    {
        if (!string.IsNullOrWhiteSpace(option.Uci)
            && !string.IsNullOrWhiteSpace(uci)
            && string.Equals(option.Uci, uci, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            SanNotation.NormalizeSan(option.DisplayText),
            SanNotation.NormalizeSan(displayText),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildMoveKey(string? uci, string displayText)
    {
        return !string.IsNullOrWhiteSpace(uci)
            ? $"uci:{uci.Trim().ToLowerInvariant()}"
            : $"san:{SanNotation.NormalizeSan(displayText)}";
    }

    private static OpeningTrainingScore DetermineScore(
        OpeningTrainingStrictness strictness,
        bool matchedPreferred,
        bool matchedPlayable,
        bool transposedToKnownPosition)
    {
        return strictness switch
        {
            OpeningTrainingStrictness.StrictRepertoire => matchedPreferred ? OpeningTrainingScore.Correct : OpeningTrainingScore.Wrong,
            OpeningTrainingStrictness.BookFlexible => matchedPreferred
                ? OpeningTrainingScore.Correct
                : matchedPlayable || transposedToKnownPosition
                    ? OpeningTrainingScore.Playable
                    : OpeningTrainingScore.Wrong,
            OpeningTrainingStrictness.EngineTolerant => matchedPreferred
                ? OpeningTrainingScore.Correct
                : matchedPlayable || transposedToKnownPosition
                    ? OpeningTrainingScore.Playable
                    : OpeningTrainingScore.Wrong,
            OpeningTrainingStrictness.Exploration => matchedPreferred
                ? OpeningTrainingScore.Correct
                : OpeningTrainingScore.Playable,
            _ => matchedPreferred ? OpeningTrainingScore.Correct : matchedPlayable ? OpeningTrainingScore.Playable : OpeningTrainingScore.Wrong
        };
    }

    public static string? GetPreferredTheoryMoveDisplay(IReadOnlyList<OpeningTrainingMoveOption> options)
    {
        return options
            .FirstOrDefault(option => option.IsPreferred)?.DisplayText
            ?? (options.Count > 0 ? options[0] : null)?.DisplayText;
    }

    public static string EnsureSentence(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        char last = trimmed[^1];
        return last is '.' or '!' or '?'
            ? trimmed
            : $"{trimmed}.";
    }

    private static string FormatMatchedReferences(IReadOnlyList<OpeningTrainingMoveOption> options)
    {
        return string.Join(", ", options.Select(option => option.DisplayText).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static PlayerSide Opponent(PlayerSide side)
    {
        return side == PlayerSide.White ? PlayerSide.Black : PlayerSide.White;
    }
}
