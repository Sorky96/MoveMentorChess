using System.Collections.Generic;
using MoveMentorChess.Domain;
using MoveMentorChess.App.ViewModels;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class AnalysisMistakeItemViewModelTests
{
    [Fact]
    public void Constructor_WithValidMoves_InitializesPropertiesCorrectly()
    {
        // Arrange
        var ply = new ReplayPly(
            1,
            1,
            PlayerSide.White,
            "e4",
            "e4",
            "e2e4",
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            GamePhase.Opening,
            "P",
            null,
            "e2",
            "e4",
            IsCapture: false,
            IsEnPassant: false,
            IsCastle: false);

        var before = new EngineAnalysis(ply.FenBefore, [], null);
        var after = new EngineAnalysis(ply.FenAfter, [], null);

        var moveAnalysis = new MoveAnalysisResult(
            ply,
            before,
            after,
            EvalBeforeCp: 40,
            EvalAfterCp: 30,
            BestMateIn: null,
            PlayedMateIn: null,
            CentipawnLoss: 10,
            Quality: MoveQualityBucket.Inaccuracy,
            MaterialDeltaCp: 0,
            MistakeTag: new MistakeTag("king_safety", 0.8, []),
            Explanation: new MoveExplanation("short explanation", "hint text"));

        var mistake = new SelectedMistake(
            [moveAnalysis],
            MoveQualityBucket.Inaccuracy,
            new MistakeTag("king_safety", 0.8, []),
            new MoveExplanation("mistake short", "mistake hint"));

        // Act
        var viewModel = new AnalysisMistakeItemViewModel(mistake);

        // Assert
        Assert.Equal(mistake, viewModel.Mistake);
        Assert.Equal(moveAnalysis, viewModel.LeadMove);
        Assert.Contains("1. e4", viewModel.DisplayText);
        Assert.Contains("Inaccuracy", viewModel.DisplayText);
        Assert.Contains("king_safety", viewModel.DisplayText);
        Assert.Contains("CPL 10", viewModel.DisplayText);
        Assert.Contains("Moves: 1. e4", viewModel.Details);
    }

    [Fact]
    public void Constructor_WithEmptyMoves_AssignsSafeDefaultsAndReturnsEarly()
    {
        // Arrange
        var mistake = new SelectedMistake(
            [],
            MoveQualityBucket.Blunder,
            new MistakeTag("hanging_piece", 0.9, []),
            new MoveExplanation("mistake short", "mistake hint"));

        // Act
        var viewModel = new AnalysisMistakeItemViewModel(mistake);

        // Assert
        Assert.Equal(mistake, viewModel.Mistake);
        Assert.Null(viewModel.LeadMove);
        Assert.Equal("No moves", viewModel.DisplayText);
        Assert.Equal("No moves available", viewModel.Details);
    }

    [Fact]
    public void Constructor_WithNullMoves_AssignsSafeDefaultsAndReturnsEarly()
    {
        // Arrange
        var mistake = new SelectedMistake(
            null!,
            MoveQualityBucket.Blunder,
            new MistakeTag("hanging_piece", 0.9, []),
            new MoveExplanation("mistake short", "mistake hint"));

        // Act
        var viewModel = new AnalysisMistakeItemViewModel(mistake);

        // Assert
        Assert.Equal(mistake, viewModel.Mistake);
        Assert.Null(viewModel.LeadMove);
        Assert.Equal("No moves", viewModel.DisplayText);
        Assert.Equal("No moves available", viewModel.Details);
    }
}
