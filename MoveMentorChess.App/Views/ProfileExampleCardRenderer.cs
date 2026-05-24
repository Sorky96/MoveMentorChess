using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Profiles;
using static MoveMentorChess.App.ViewModels.ProfileCoachPresentationText;

namespace MoveMentorChess.App.Views;

internal sealed class ProfileExampleCardRenderer(
    Window owner,
    ProfileCoachSessionTracker profileSessionTracker,
    Func<ProfileMistakeExample, Task>? navigateToProfileExampleAsync,
    Func<OpeningExampleGame, Task>? navigateToOpeningExampleAsync,
    Func<OpeningMoveRecommendation, Task>? navigateToOpeningPositionAsync,
    Func<bool> isClosed,
    Action closeWindow)
{
    public Control CreateExampleCard(ProfileMistakeExample example, bool compact = false)
    {
        Border card = new()
        {
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 10)
        };

        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions(compact ? "120,*" : "160,*")
        };

        grid.Children.Add(ProfileBoardPreviewRenderer.CreateLazyBoardPreview(example.FenBefore, compact ? 120 : 160, []));

        StackPanel panel = new()
        {
            Margin = new Thickness(14, 0, 0, 0),
            Spacing = 6
        };
        Grid.SetColumn(panel, 1);

        panel.Children.Add(new TextBlock
        {
            Text = $"Move {example.MoveNumber}{(example.Side == PlayerSide.White ? "." : "...")} {example.PlayedSan}",
            FontSize = compact ? 16 : 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(CreateBodyText(FormatExampleRank(example.Rank), "#9EB5C5"));
        panel.Children.Add(CreateBodyText($"Better move: {example.BetterMove}", "#D7E2EA"));
        panel.Children.Add(CreateBodyText($"Label: {FormatMistakeLabel(example.Label)} | CPL: {example.CentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} | Phase: {FormatPhase(example.Phase)}", "#D7E2EA"));
        panel.Children.Add(CreateBodyText($"Opening: {FormatOpening(example.Eco)}", "#D7E2EA"));

        Button button = new()
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0),
            Content = "Go to Analysis",
            IsEnabled = navigateToProfileExampleAsync is not null
        };
        button.Click += async (_, _) =>
        {
            if (navigateToProfileExampleAsync is null)
            {
                return;
            }

            profileSessionTracker.TrackActionClicked("Go to Analysis");
            button.IsEnabled = false;
            try
            {
                await navigateToProfileExampleAsync(example);
                closeWindow();
            }
            finally
            {
                if (!isClosed())
                {
                    button.IsEnabled = true;
                }
            }
        };
        panel.Children.Add(button);

        grid.Children.Add(panel);
        card.Child = grid;
        return card;
    }

    public Control CreateOpeningExampleCard(OpeningExampleGame example)
    {
        Border card = new()
        {
            Background = Brush.Parse("#203542"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };

        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{example.OpponentName} | {example.DateText ?? "Unknown date"} | {example.Result ?? "?"}",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(CreateBodyText(
            $"First mistake: {FormatMistakeLabel(example.FirstMistakeType ?? "unclassified")} on {FormatPlyLabel(example.Side, example.FirstMistakePly, example.FirstMistakeSan)}",
            "#D7E2EA"));
        panel.Children.Add(CreateBodyText(
            $"Opening: {example.OpeningDisplayName} | CPL {example.FirstMistakeCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}",
            "#D7E2EA"));

        Button button = new()
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = "Open game",
            IsEnabled = navigateToOpeningExampleAsync is not null
        };
        button.Click += async (_, _) =>
        {
            if (navigateToOpeningExampleAsync is null)
            {
                return;
            }

            profileSessionTracker.TrackActionClicked("Open game");
            button.IsEnabled = false;
            try
            {
                await navigateToOpeningExampleAsync(example);
                closeWindow();
            }
            finally
            {
                if (!isClosed())
                {
                    button.IsEnabled = true;
                }
            }
        };
        panel.Children.Add(button);

        card.Child = panel;
        return card;
    }

    public Control CreateOpeningPositionCard(OpeningMoveRecommendation recommendation)
    {
        Border card = new()
        {
            Background = Brush.Parse("#203542"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };

        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("120,*")
        };

        IReadOnlyList<BoardArrowViewModel> arrows = ProfileBoardPreviewRenderer.BuildPreviewArrows(
            recommendation.FenBefore,
            (recommendation.PlayedSan, Color.Parse("#F6C453")),
            (recommendation.BetterMove, Color.Parse("#58D68D")));
        Control boardHost = ProfileBoardPreviewRenderer.CreateBoardPreview(
            recommendation.FenBefore,
            120,
            arrows,
            async () => await ProfileBoardPreviewRenderer.ShowBoardPreviewWindowAsync(
                owner: owner,
                title: $"Opening Position | {OpeningCatalog.Describe(recommendation.Eco)}",
                fen: recommendation.FenBefore,
                arrows: arrows,
                detailLines: ProfileBoardPreviewRenderer.BuildRecommendationPreviewDetailLines(recommendation)));
        grid.Children.Add(boardHost);

        StackPanel panel = new()
        {
            Margin = new Thickness(14, 0, 0, 0),
            Spacing = 6
        };
        Grid.SetColumn(panel, 1);

        panel.Children.Add(new TextBlock
        {
            Text = FormatPlyLabel(recommendation.Side, recommendation.Ply, recommendation.PlayedSan),
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(CreateBodyText($"Your move: {recommendation.PlayedSan}", "#9EB5C5"));
        panel.Children.Add(CreateBodyText($"Suggested move: {recommendation.BetterMove}", "#D7E2EA"));
        panel.Children.Add(CreateBodyText(
            $"Theme: {FormatMistakeLabel(recommendation.MistakeType ?? "unclassified")} | CPL {recommendation.CentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}",
            "#D7E2EA"));

        Button button = new()
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = "Open position",
            IsEnabled = navigateToOpeningPositionAsync is not null
        };
        button.Click += async (_, _) =>
        {
            if (navigateToOpeningPositionAsync is null)
            {
                return;
            }

            profileSessionTracker.TrackActionClicked("Open position", new Dictionary<string, string>
            {
                ["opening"] = recommendation.Eco
            });
            button.IsEnabled = false;
            try
            {
                await navigateToOpeningPositionAsync(recommendation);
                closeWindow();
            }
            finally
            {
                if (!isClosed())
                {
                    button.IsEnabled = true;
                }
            }
        };
        panel.Children.Add(button);

        grid.Children.Add(panel);
        card.Child = grid;
        return card;
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
