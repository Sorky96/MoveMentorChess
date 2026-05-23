using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MoveMentorChess.Analysis;
using MoveMentorChess.App.Controls;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Profiles;
using static MoveMentorChess.App.ViewModels.ProfileCoachPresentationText;

namespace MoveMentorChess.App.Views;

internal static class ProfileBoardPreviewRenderer
{
    public static Control CreateBoardPreview(
        string fen,
        double size,
        IReadOnlyList<BoardArrowViewModel> arrows,
        Func<Task>? onClick = null)
        => CreateLazyBoardPreview(fen, size, arrows, onClick, "Show board");

    public static Control CreateLazyBoardPreview(
        string fen,
        double size,
        IReadOnlyList<BoardArrowViewModel> arrows,
        Func<Task>? onClick = null,
        string buttonText = "Show board")
    {
        Border boardHost = new()
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true
        };

        Control placeholder = onClick is null
            ? new Button
            {
                Content = buttonText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = Math.Min(110, Math.Max(80, size - 24))
            }
            : new TextBlock
            {
                Text = buttonText,
                Foreground = Brush.Parse("#D7E2EA"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
        boardHost.Child = placeholder;

        void RevealBoard()
        {
            boardHost.Child = new ChessBoardView
            {
                Width = size,
                Height = size,
                Fen = fen,
                Arrows = arrows,
                IsHitTestVisible = false
            };
        }

        if (placeholder is Button revealButton)
        {
            revealButton.Click += (_, _) => RevealBoard();
        }

        if (onClick is null)
        {
            return boardHost;
        }

        Button button = new()
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = boardHost
        };
        button.Click += async (_, _) =>
        {
            if (boardHost.Child is not ChessBoardView)
            {
                RevealBoard();
            }

            await onClick();
        };
        return button;
    }

    public static async Task ShowBoardPreviewWindowAsync(
        Window owner,
        string title,
        string fen,
        IReadOnlyList<BoardArrowViewModel> arrows,
        IReadOnlyList<string> detailLines)
    {
        Window window = new()
        {
            Title = title,
            Width = 980,
            Height = 780,
            MinWidth = 760,
            MinHeight = 620,
            Background = Brush.Parse("#23313B")
        };

        StackPanel rightPanel = new()
        {
            Spacing = 8
        };

        foreach (string line in detailLines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            rightPanel.Children.Add(CreateBodyText(line, "#D7E2EA"));
        }

        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("460,*")
        };

        Border boardCard = new()
        {
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16),
            Child = new ChessBoardView
            {
                Width = 420,
                Height = 420,
                Fen = fen,
                Arrows = arrows,
                IsHitTestVisible = false
            }
        };
        grid.Children.Add(boardCard);

        Border detailsCard = new()
        {
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(18, 0, 0, 0),
            Padding = new Thickness(16),
            Child = new ScrollViewer
            {
                Content = rightPanel
            }
        };
        Grid.SetColumn(detailsCard, 1);
        grid.Children.Add(detailsCard);

        window.Content = new Border
        {
            Padding = new Thickness(18),
            Child = grid
        };

        await window.ShowDialog(owner);
    }

    public static IReadOnlyList<string> BuildRecommendationPreviewDetailLines(OpeningMoveRecommendation recommendation)
    {
        return
        [
            FormatPlyLabel(recommendation.Side, recommendation.Ply, recommendation.PlayedSan),
            $"Your move: {recommendation.PlayedSan}",
            $"Suggested move: {recommendation.BetterMove}",
            $"Theme: {FormatMistakeLabel(recommendation.MistakeType ?? "unclassified")} | CPL {recommendation.CentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}"
        ];
    }

    public static List<BoardArrowViewModel> BuildPreviewArrows(
        string fen,
        params (string? MoveText, Color Color)[] moveSpecs)
    {
        List<BoardArrowViewModel> arrows = [];
        foreach ((string? moveText, Color color) in moveSpecs)
        {
            if (TryBuildArrow(fen, moveText, color, out BoardArrowViewModel arrow))
            {
                arrows.Add(arrow);
            }
        }

        return arrows;
    }

    private static bool TryBuildArrow(string fen, string? moveText, Color color, out BoardArrowViewModel arrow)
    {
        arrow = default!;
        if (string.IsNullOrWhiteSpace(fen) || string.IsNullOrWhiteSpace(moveText))
        {
            return false;
        }

        ChessGame game = new();
        if (!game.TryLoadFen(fen, out _))
        {
            return false;
        }

        if (TryApplyPreviewMove(game, moveText, out AppliedMoveInfo? appliedMove) && appliedMove is not null)
        {
            arrow = new BoardArrowViewModel(appliedMove.FromSquare, appliedMove.ToSquare, color);
            return true;
        }

        return false;
    }

    private static bool TryApplyPreviewMove(ChessGame game, string moveText, out AppliedMoveInfo? appliedMove)
    {
        appliedMove = null;
        string trimmed = moveText.Trim();
        string? uci = TryExtractUci(trimmed);
        if (!string.IsNullOrWhiteSpace(uci) && game.TryApplyUci(uci, out appliedMove, out _))
        {
            return appliedMove is not null;
        }

        string san = TrimMoveDisplayText(trimmed);
        try
        {
            appliedMove = game.ApplySanWithResult(san);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

#pragma warning disable SYSLIB1045
    private static string? TryExtractUci(string moveText)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(moveText, "^[a-h][1-8][a-h][1-8][qrbn]?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return moveText;
        }

        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            moveText,
            "\\(([a-h][1-8][a-h][1-8][qrbn]?)\\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
#pragma warning restore SYSLIB1045

    private static string TrimMoveDisplayText(string moveText)
    {
        int parenIndex = moveText.IndexOf(" (", StringComparison.Ordinal);
        return parenIndex > 0 ? moveText[..parenIndex].Trim() : moveText;
    }

    private static TextBlock CreateBodyText(string text, string? color = null)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color is null ? Brushes.White : Brush.Parse(color),
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap
        };
    }
}
