using MoveMentorChess.Opening;
using Xunit;

namespace MoveMentorChessServices.Tests.Profiles;

public sealed class OpeningTheoryBranchBuilderTests
{
    private const string RootFen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
    private const string AfterC5Fen = "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2";
    private const string AfterE5Fen = "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2";
    private const string AfterNf3Fen = "rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2";

    [Fact]
    public void GetTheoryMoves_PrefersPlayableMovesOverTopMoves()
    {
        FakeTheoryStore store = new();
        OpeningTheoryMove playable = CreateMove("c7c5", "c5", RootFen, AfterC5Fen, isPlayable: true);
        OpeningTheoryMove top = CreateMove("e7e5", "e5", RootFen, AfterE5Fen, isPlayable: false);
        store.AddMove(RootFen, playable);
        store.AddMove(RootFen, top);
        OpeningTheoryQueryService theory = new(store);

        IReadOnlyList<OpeningTheoryMove> moves = OpeningTheoryBranchBuilder.GetTheoryMoves(theory, RootFen, limit: 3);

        OpeningTheoryMove move = Assert.Single(moves);
        Assert.Equal("c7c5", move.MoveUci);
    }

    [Fact]
    public void BuildBranches_CreatesTheoryBranchesWithRecommendedReply()
    {
        FakeTheoryStore store = new();
        store.AddMove(RootFen, CreateMove("c7c5", "c5", RootFen, AfterC5Fen, isMainMove: true, distinctGames: 4, occurrences: 7));
        store.AddMove(AfterC5Fen, CreateMove("g1f3", "Nf3", AfterC5Fen, AfterNf3Fen, isMainMove: true, distinctGames: 3, occurrences: 5));
        OpeningTheoryQueryService theory = new(store);

        List<OpeningTrainingBranch> branches = OpeningTheoryBranchBuilder.BuildBranches(RootFen, theory);

        OpeningTrainingBranch branch = Assert.Single(branches);
        Assert.Equal("c5", branch.OpponentMove);
        Assert.Equal("c7c5", branch.OpponentMoveUci);
        Assert.Equal(4, branch.Frequency);
        Assert.Contains("Main imported branch", branch.SourceSummary, StringComparison.Ordinal);
        Assert.NotNull(branch.RecommendedResponse);
        Assert.Equal("Nf3", branch.RecommendedResponse!.DisplayText);
        Assert.Equal(2, branch.Continuation.Count);
        Assert.Equal(PlayerSide.Black, branch.Continuation[0].Side);
        Assert.Equal(PlayerSide.White, branch.Continuation[1].Side);
    }

    [Fact]
    public void BuildOpponentReplyProfile_UsesTheoryBranchFrequencies()
    {
        OpeningTrainingBranch branch = new(
            new OpeningBranchKey("branch"),
            "c5",
            "c7c5",
            4,
            "Imported branch",
            null,
            [],
            [],
            new OpeningPositionKey("after-c5"));

        OpponentReplyProfile profile = OpeningTheoryBranchBuilder.BuildOpponentReplyProfile(
            new OpeningLineKey("line"),
            RepertoireSide.White,
            [branch]);
        OpeningCoverageSummary coverage = OpeningTheoryBranchBuilder.BuildCoverageSummary([branch]);

        Assert.Equal("Prepared 1 opponent branch(es) from theory.", profile.Summary);
        OpponentMoveFrequency frequency = Assert.Single(profile.Frequencies);
        Assert.Equal("c5", frequency.MoveSan);
        Assert.Equal(4, frequency.Weight);
        Assert.Equal(1, coverage.TotalBookBranches);
        Assert.Equal(1, coverage.WeakBranches);
    }

    private static OpeningTheoryMove CreateMove(
        string uci,
        string san,
        string fromFen,
        string toFen,
        bool isMainMove = false,
        bool isPlayable = false,
        int distinctGames = 1,
        int occurrences = 1)
    {
        string toKey = OpeningPositionKeyBuilder.BuildKey(toFen).Value;
        return new OpeningTheoryMove(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            uci,
            san,
            occurrences,
            distinctGames,
            isMainMove,
            isPlayable,
            1,
            toKey,
            new OpeningPositionKey(toKey),
            toFen,
            OpeningGameMetadata.Empty,
            Idea: null);
    }

    private sealed class FakeTheoryStore : IOpeningTheoryStore
    {
        private readonly Dictionary<string, List<OpeningTheoryMove>> movesByPosition = new(StringComparer.Ordinal);

        public void AddMove(string fen, OpeningTheoryMove move)
        {
            string key = OpeningPositionKeyBuilder.BuildKey(fen).Value;
            if (!movesByPosition.TryGetValue(key, out List<OpeningTheoryMove>? moves))
            {
                moves = [];
                movesByPosition[key] = moves;
            }

            moves.Add(move);
        }

        public bool TryGetOpeningPositionByKey(string positionKey, out OpeningTheoryPosition? position)
        {
            position = null;
            return false;
        }

        public IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(
            string positionKey,
            int limit = 10,
            bool playableOnly = false)
        {
            if (!movesByPosition.TryGetValue(positionKey, out List<OpeningTheoryMove>? moves))
            {
                return [];
            }

            return moves
                .Where(move => !playableOnly || move.IsPlayableMove)
                .Take(limit)
                .ToList();
        }
    }
}
