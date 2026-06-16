using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MoveMentorChess.App.Composition;
using MoveMentorChess.App.Renderers;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Profiles;
using static MoveMentorChess.App.ViewModels.ProfileCoachPresentationText;
using static MoveMentorChess.App.Renderers.ProfileCoachSectionRenderer;

namespace MoveMentorChess.App.Views;

public partial class ProfilesWindow : Window
{
    private const string TrainerPreparingSuggestionsText = "Your personal trainer is preparing suggestions...";

    private readonly IProfilesWindowDataService dataService;
    private readonly PlayerProfileService profileService;
    private readonly IProfileFormattingWorkflow profileFormattingWorkflow;
    private readonly ProfileCoachSessionTracker profileSessionTracker;
    private readonly ProfileExampleCardRenderer exampleCardRenderer;
    private List<PlayerProfileSummaryItemViewModel> items = [];
    private string? currentProfilePlayerKey;
    private PlayerProfileReport? currentReport;
    private OpeningWeaknessReport? currentOpeningReport;
    private ProfileViewMode currentViewMode = ProfileViewMode.Coach;
    private bool isProfilesPanelVisible = true;
    private int profileRenderVersion;

    public ProfilesWindow()
        : this(new DefaultProfilesWindowDataService(() => null))
    {
    }

    public ProfilesWindow(
        PlayerProfileService profileService,
        Func<ProfileMistakeExample, Task>? navigateToProfileExampleAsync = null,
        Func<OpeningExampleGame, Task>? navigateToOpeningExampleAsync = null,
        Func<OpeningMoveRecommendation, Task>? navigateToOpeningPositionAsync = null,
        IPlayerProfileFormatter? profileFormatter = null,
        ITrainingPlanFormatter? trainingPlanFormatter = null)
        : this(
            new ProvidedProfilesWindowDataService(profileService),
            navigateToProfileExampleAsync,
            navigateToOpeningExampleAsync,
            navigateToOpeningPositionAsync,
            new DefaultProfileFormattingWorkflow(profileFormatter, trainingPlanFormatter))
    {
    }

    internal ProfilesWindow(
        IProfilesWindowDataService dataService,
        Func<ProfileMistakeExample, Task>? navigateToProfileExampleAsync = null,
        Func<OpeningExampleGame, Task>? navigateToOpeningExampleAsync = null,
        Func<OpeningMoveRecommendation, Task>? navigateToOpeningPositionAsync = null,
        IProfileFormattingWorkflow? profileFormattingWorkflow = null)
    {
        this.dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        profileService = dataService.ProfileService;
        this.profileFormattingWorkflow = profileFormattingWorkflow ?? new DefaultProfileFormattingWorkflow();
        profileSessionTracker = dataService.CreateSessionTracker();
        exampleCardRenderer = new ProfileExampleCardRenderer(
            this,
            profileSessionTracker,
            navigateToProfileExampleAsync,
            navigateToOpeningExampleAsync,
            navigateToOpeningPositionAsync,
            IsClosed,
            Close);
        InitializeComponent();
        UpdateViewModeButtons();
        UpdateProfilesPanelVisibility();
        RefreshList();
    }

    private void FilterTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshList();
    }

    private async void ProfilesListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProfilesListBox.SelectedItem is not PlayerProfileSummaryItemViewModel item)
        {
            ShowStatus("Select a player to inspect the profile.");
            return;
        }

        string requestedPlayerKey = item.Summary.PlayerKey;
        int renderVersion = ++profileRenderVersion;
        ShowProfileLoadingStatus(item.Summary.DisplayName);

        try
        {
            (PlayerProfileReport? Report, OpeningWeaknessReport? OpeningReport) result = await Task.Run<(PlayerProfileReport? Report, OpeningWeaknessReport? OpeningReport)>(() =>
            {
                if (!profileService.TryBuildProfile(requestedPlayerKey, out PlayerProfileReport? builtReport) || builtReport is null)
                {
                    return (null, null);
                }

                profileService.TryBuildOpeningWeaknessReport(requestedPlayerKey, out OpeningWeaknessReport? builtOpeningReport);
                return (builtReport, builtOpeningReport);
            });

            if (renderVersion != profileRenderVersion
                || IsClosed()
                || ProfilesListBox.SelectedItem is not PlayerProfileSummaryItemViewModel selectedItem
                || !string.Equals(selectedItem.Summary.PlayerKey, requestedPlayerKey, StringComparison.Ordinal))
            {
                return;
            }

            if (result.Report is null)
            {
                ShowStatus("Could not load the selected player profile.");
                return;
            }

            RenderProfile(result.Report, result.OpeningReport, renderVersion);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ShowStatus("Could not load the selected player profile.");
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        profileSessionTracker.TrackClosed("back_to_board");
        Close();
    }

    private void ToggleProfilesButton_Click(object? sender, RoutedEventArgs e)
    {
        isProfilesPanelVisible = !isProfilesPanelVisible;
        UpdateProfilesPanelVisibility();
        profileSessionTracker.Track("profile_players_panel_toggled", new Dictionary<string, string>
        {
            ["is_visible"] = isProfilesPanelVisible.ToString().ToLowerInvariant()
        });
    }

    private void CoachModeButton_Click(object? sender, RoutedEventArgs e)
    {
        SwitchViewMode(ProfileViewMode.Coach);
    }

    private void EvidenceModeButton_Click(object? sender, RoutedEventArgs e)
    {
        SwitchViewMode(ProfileViewMode.Evidence);
    }

    private void SwitchViewMode(ProfileViewMode viewMode)
    {
        if (currentViewMode == viewMode)
        {
            return;
        }

        currentViewMode = viewMode;
        profileSessionTracker.SetViewMode(currentViewMode);
        UpdateViewModeButtons();
        profileSessionTracker.Track("profile_view_mode_selected", new Dictionary<string, string>
        {
            ["view_mode"] = currentViewMode.ToString()
        });

        if (currentReport is not null)
        {
            RenderProfile(currentReport, currentOpeningReport);
        }
    }

    private void DetailsScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        double scrollableHeight = Math.Max(0, DetailsScrollViewer.Extent.Height - DetailsScrollViewer.Viewport.Height);
        if (scrollableHeight <= 0)
        {
            return;
        }

        profileSessionTracker.RecordScrollDepth(DetailsScrollViewer.Offset.Y / scrollableHeight);
    }

    private void UpdateViewModeButtons()
    {
        CoachModeButton.Background = Brush.Parse(currentViewMode == ProfileViewMode.Coach ? "#355D73" : "#203542");
        EvidenceModeButton.Background = Brush.Parse(currentViewMode == ProfileViewMode.Evidence ? "#355D73" : "#203542");
    }

    private void UpdateProfilesPanelVisibility()
    {
        ProfilesPanel.IsVisible = isProfilesPanelVisible;
        ProfilesLayout.ColumnDefinitions[0].Width = isProfilesPanelVisible ? new GridLength(300) : new GridLength(0);
        DetailsHost.Margin = isProfilesPanelVisible ? new Thickness(14, 0, 0, 0) : new Thickness(0);
        ToggleProfilesButton.Content = isProfilesPanelVisible ? "Hide players" : "Show players";
    }

    private void RefreshList()
    {
        items = profileService.ListProfiles(FilterTextBox.Text)
            .Select(summary => new PlayerProfileSummaryItemViewModel(summary))
            .ToList();
        ProfilesListBox.ItemsSource = items;

        if (items.Count > 0)
        {
            ProfilesListBox.SelectedIndex = 0;
        }
        else
        {
            ShowStatus(BuildEmptyStateMessage());
        }
    }

    private string BuildEmptyStateMessage()
    {
        ProfileDataAvailability availability = profileService.GetDataAvailability(FilterTextBox.Text);
        ProfileDataAvailability totalAvailability = profileService.GetDataAvailability();
        if (availability.AnalyzedProfiles > 0)
        {
            return "No matching analyzed player. Try another name or clear the search.";
        }

        if (!string.IsNullOrWhiteSpace(FilterTextBox.Text) && totalAvailability.AnalyzedProfiles > 0)
        {
            return "No matching analyzed player. Try another name or clear the search.";
        }

        return "No player profile yet. Analyze at least one saved game, then come back here to see recurring mistakes, opening issues, and a weekly training plan.";
    }

    private void ShowStatus(string text)
    {
        DetailsPanel.Children.Clear();
        DetailsPanel.Children.Add(CreateSectionCard(
            "Player Coach",
            [
                CreateBodyText(text)
            ]));
    }

    private void RenderProfile(PlayerProfileReport report, OpeningWeaknessReport? openingReport)
        => RenderProfile(report, openingReport, ++profileRenderVersion);

    private void RenderProfile(PlayerProfileReport report, OpeningWeaknessReport? openingReport, int renderVersion)
    {
        DetailsPanel.Children.Clear();
        if (currentReport is not null && !string.Equals(currentReport.PlayerKey, report.PlayerKey, StringComparison.Ordinal))
        {
            profileSessionTracker.TrackClosed("profile_changed");
        }

        currentProfilePlayerKey = report.PlayerKey;
        currentReport = report;
        currentOpeningReport = openingReport;
        profileSessionTracker.StartProfile(report, currentProfilePlayerKey, currentViewMode);
        StackPanel summaryPanel = BuildRowsPanel(BuildFormattedProfilePlaceholderRows());
        StackPanel weeklyPlanPanel = BuildRowsPanel(BuildWeeklyPlanRows(report, CreateTrainingPlanPlaceholder(), PracticeOpeningFromProfileAsync));
        StackPanel compactWeeklyPlanPanel = BuildRowsPanel(BuildCompactWeeklyPlanRows(report, CreateTrainingPlanPlaceholder(), PracticeOpeningFromProfileAsync));

        if (currentViewMode == ProfileViewMode.Coach)
        {
            RenderCoachView(report, openingReport, summaryPanel, compactWeeklyPlanPanel, weeklyPlanPanel);
        }
        else
        {
            RenderEvidenceView(report, openingReport, summaryPanel, weeklyPlanPanel);
        }

        _ = RenderFormattedProfileAsync(report, renderVersion, summaryPanel, weeklyPlanPanel, compactWeeklyPlanPanel);
        profileSessionTracker.TrackOpened();
    }

    private void RenderCoachView(
        PlayerProfileReport report,
        OpeningWeaknessReport? openingReport,
        StackPanel summaryPanel,
        StackPanel compactWeeklyPlanPanel,
        StackPanel weeklyPlanPanel)
    {
        DetailsPanel.Children.Add(CreateHeroCard(report));
        DetailsPanel.Children.Add(CreateCoachDecisionCard(report));
        DetailsPanel.Children.Add(CreateMetricsCard(report));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Coach summary", summaryPanel, isExpanded: true));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Priorities", BuildPriorityRows(report), isExpanded: true));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Weekly plan", compactWeeklyPlanPanel, isExpanded: true));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Weekly plan details", weeklyPlanPanel));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Evidence snapshot", BuildEvidenceSnapshotRows(report, openingReport)));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Opening weaknesses", BuildOpeningWeaknessRows(
            openingReport,
            !string.IsNullOrWhiteSpace(currentProfilePlayerKey),
            exampleCardRenderer.CreateOpeningExampleCard,
            exampleCardRenderer.CreateOpeningPositionCard,
            PracticeOpeningFromProfileAsync)));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Mistake patterns", BuildMistakePatternRows(report)));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Example positions", BuildExampleRows(report, exampleCardRenderer.CreateExampleCard)));
    }

    private void RenderEvidenceView(
        PlayerProfileReport report,
        OpeningWeaknessReport? openingReport,
        StackPanel summaryPanel,
        StackPanel weeklyPlanPanel)
    {
        DetailsPanel.Children.Add(CreateHeroCard(report));
        DetailsPanel.Children.Add(CreateSnapshotCard(report));
        DetailsPanel.Children.Add(CreateMetricsCard(report));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Coach summary", summaryPanel));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Form and strength", BuildRatingAndFormRows(report), isExpanded: true));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Opening weaknesses", BuildOpeningWeaknessRows(
            openingReport,
            !string.IsNullOrWhiteSpace(currentProfilePlayerKey),
            exampleCardRenderer.CreateOpeningExampleCard,
            exampleCardRenderer.CreateOpeningPositionCard,
            PracticeOpeningFromProfileAsync), isExpanded: true));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Mistake patterns", BuildMistakePatternRows(report), isExpanded: true));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Weekly plan details", weeklyPlanPanel));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Recent form", BuildRecentTrendRows(report)));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Why this matters", BuildDeepDiveRows(report, exampleCardRenderer.CreateExampleCard)));
        DetailsPanel.Children.Add(CreateCollapsibleSection("Example positions", BuildExampleRows(report, exampleCardRenderer.CreateExampleCard)));
    }

    private void ShowProfileLoadingStatus(string displayName)
    {
        DetailsPanel.Children.Clear();
        DetailsPanel.Children.Add(CreateSectionCard(
            "Player Coach",
            [
                CreateBodyText($"Loading {displayName}'s profile...", "#D7E2EA"),
                CreateBodyText(TrainerPreparingSuggestionsText, "#9EB5C5")
            ]));
    }

    private IEnumerable<Control> BuildFormattedProfileRows(PlayerProfileFormattedOutput output)
    {
        yield return CreateBodyText(output.ProfileSummary);
        yield return CreateBulletText(output.StrengthsAndWeaknesses);
        yield return CreateBulletText(output.WhatToFocusNext);
        yield return CreateBulletText(output.ToneAdaptedVersion);

        if (!string.IsNullOrWhiteSpace(output.DeepDive))
        {
            yield return CreateBodyText(output.DeepDive);
        }
    }

    private IEnumerable<Control> BuildFormattedProfilePlaceholderRows()
    {
        yield return CreateBodyText(TrainerPreparingSuggestionsText, "#D7E2EA");
    }

    private static TrainingPlanFormattedOutput CreateTrainingPlanPlaceholder()
    {
        return new TrainingPlanFormattedOutput(
            TrainerPreparingSuggestionsText,
            TrainerPreparingSuggestionsText,
            TrainerPreparingSuggestionsText,
            TrainerPreparingSuggestionsText);
    }

    private async Task RenderFormattedProfileAsync(
        PlayerProfileReport report,
        int renderVersion,
        StackPanel summaryPanel,
        StackPanel weeklyPlanPanel,
        StackPanel? compactWeeklyPlanPanel = null)
    {
        try
        {
            ProfileFormattingResult formatted = await profileFormattingWorkflow.FormatAsync(report);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (renderVersion != profileRenderVersion
                    || !string.Equals(currentProfilePlayerKey, report.PlayerKey, StringComparison.Ordinal)
                    || IsClosed())
                {
                    return;
                }

                ReplacePanelChildren(summaryPanel, BuildFormattedProfileRows(formatted.Profile));
                ReplacePanelChildren(weeklyPlanPanel, BuildWeeklyPlanRows(report, formatted.TrainingPlan, PracticeOpeningFromProfileAsync));
                if (compactWeeklyPlanPanel is not null)
                {
                    ReplacePanelChildren(compactWeeklyPlanPanel, BuildCompactWeeklyPlanRows(report, formatted.TrainingPlan, PracticeOpeningFromProfileAsync));
                }
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (renderVersion != profileRenderVersion || IsClosed())
                {
                    return;
                }

                ProfileFormattingResult fallback = profileFormattingWorkflow.FormatFallback(report);
                ReplacePanelChildren(summaryPanel, BuildFormattedProfileRows(fallback.Profile));
                ReplacePanelChildren(weeklyPlanPanel, BuildWeeklyPlanRows(report, fallback.TrainingPlan, PracticeOpeningFromProfileAsync));
                if (compactWeeklyPlanPanel is not null)
                {
                    ReplacePanelChildren(compactWeeklyPlanPanel, BuildCompactWeeklyPlanRows(report, fallback.TrainingPlan, PracticeOpeningFromProfileAsync));
                }
            });
        }
    }

    private Border CreateCoachDecisionCard(PlayerProfileReport report)
    {
        string mainIssue = report.TopMistakeLabels.Count > 0
            ? FormatMistakeLabel(report.TopMistakeLabels[0].Label)
            : "No dominant issue yet";
        string trend = FormatTrendHeadline(report.ProgressSignal.Direction);
        var fixFirstItems = BuildFixFirstItems(report);
        string firstAction = fixFirstItems.Count > 0
            ? fixFirstItems[0]
            : "Review two recent mistakes from your own games before the next training session.";
        string whyItMatters = BuildCoachOverviewReason(report, trend);
        string? firstOpening = FindFirstTrainingOpening(report);

        Border card = CreateCardBorder();
        StackPanel panel = CreateCardPanel();

        panel.Children.Add(CreateBodyText("Coach overview", "#9EB5C5"));
        panel.Children.Add(CreateBodyText("Main issue", "#9EB5C5"));
        panel.Children.Add(new TextBlock
        {
            Text = mainIssue,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(CreateBodyText("Why it matters", "#9EB5C5"));
        panel.Children.Add(CreateBodyText(whyItMatters, "#D7E2EA"));
        panel.Children.Add(CreateBodyText("Train this first", "#9EB5C5"));
        panel.Children.Add(CreateBodyText(firstAction, "#FFFFFF"));

        Button primaryAction = CreateSectionButton(
            string.IsNullOrWhiteSpace(firstOpening) ? "Start training" : "Practice this first",
            async () => await OpenOpeningTrainerAsync(firstOpening));
        primaryAction.Margin = new Thickness(0, 8, 0, 0);
        primaryAction.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(primaryAction);

        card.Child = panel;
        return card;
    }

    private Border CreateSectionCard(string title, IEnumerable<Control> children)
    {
        Border card = CreateCardBorder();
        StackPanel panel = CreateCardPanel();

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (Control child in children)
        {
            panel.Children.Add(child);
        }

        card.Child = panel;
        return card;
    }

    private Expander CreateCollapsibleSection(string title, IEnumerable<Control> children, bool isExpanded = false)
    {
        StackPanel? panel = isExpanded ? BuildRowsPanel(children) : null;
        Expander expander = CreateCollapsibleSectionShell(title, panel, isExpanded);
        bool contentBuilt = panel is not null;

        expander.PropertyChanged += (_, change) =>
        {
            if (change.Property != Expander.IsExpandedProperty || !expander.IsExpanded || contentBuilt)
            {
                return;
            }

            panel = BuildRowsPanel(children);
            expander.Content = CreateCollapsibleSectionContent(panel);
            contentBuilt = true;
        };

        return expander;
    }

    private Expander CreateCollapsibleSection(string title, StackPanel panel, bool isExpanded = false)
    {
        return CreateCollapsibleSectionShell(title, panel, isExpanded);
    }

    private Expander CreateCollapsibleSectionShell(string title, StackPanel? panel, bool isExpanded)
    {
        Expander expander = new()
        {
            IsExpanded = isExpanded,
            Margin = new Thickness(0, 0, 0, 10),
            Background = Brush.Parse("#203542"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = panel is null ? null : CreateCollapsibleSectionContent(panel),
            Header = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(16, 14, 16, 14),
                Child = new TextBlock
                {
                    Text = title,
                    FontSize = 19,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        expander.PropertyChanged += (_, change) =>
        {
            if (change.Property == Expander.IsExpandedProperty && expander.IsExpanded)
            {
                profileSessionTracker.Track("profile_section_expanded", new Dictionary<string, string>
                {
                    ["section"] = title,
                    ["view_mode"] = currentViewMode.ToString()
                });
            }
        };

        return expander;
    }

    private static Border CreateCollapsibleSectionContent(StackPanel panel)
    {
        return new Border
        {
            Padding = new Thickness(16, 4, 16, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = panel
        };
    }

    private StackPanel BuildRowsPanel(IEnumerable<Control> children)
    {
        StackPanel panel = CreateCardPanel();
        ReplacePanelChildren(panel, children);
        return panel;
    }

    private static void ReplacePanelChildren(StackPanel panel, IEnumerable<Control> children)
    {
        panel.Children.Clear();
        foreach (Control child in children)
        {
            panel.Children.Add(child);
        }
    }

    private static Border CreateCardBorder()
    {
        return new Border
        {
            Background = Brush.Parse("#203542"),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static StackPanel CreateCardPanel()
    {
        return new StackPanel
        {
            Spacing = 6
        };
    }

    private Button CreateSectionButton(string title, Func<Task> onClick, bool isEnabled = true)
    {
        Button button = new()
        {
            Content = title,
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 200,
            IsEnabled = isEnabled
        };
        button.Click += async (_, _) =>
        {
            profileSessionTracker.TrackActionClicked(title);

            button.IsEnabled = false;
            try
            {
                await onClick();
            }
            finally
            {
                if (!IsClosed())
                {
                    button.IsEnabled = isEnabled;
                }
            }
        };
        return button;
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

    private static TextBlock CreateBulletText(string text)
    {
        return new TextBlock
        {
            Text = $"• {text}",
            Foreground = Brushes.White,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private async Task PracticeOpeningFromProfileAsync(string eco)
    {
        profileSessionTracker.TrackActionClicked("Practice this opening", new Dictionary<string, string>
        {
            ["opening"] = eco
        });
        await OpenOpeningTrainerAsync(eco);
    }

    private async Task OpenOpeningTrainerAsync(string? openingFilter)
    {
        if (!dataService.TryCreateOpeningTrainerViewModel(out OpeningTrainerWindowViewModel? viewModel) || viewModel is null)
        {
            OpenSectionWindow(
                "Opening Trainer",
                [
                    CreateBodyText("Opening Trainer is unavailable because the local analysis store is not ready.", "#D7E2EA")
                ]);
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentProfilePlayerKey))
        {
            viewModel.AdvancedPlayerKey = currentProfilePlayerKey;
        }

        if (!string.IsNullOrWhiteSpace(openingFilter))
        {
            viewModel.FilterText = openingFilter;
            viewModel.RefreshCommand.Execute(null);
        }

        OpeningTrainerWindow window = new(viewModel)
        {
            Title = "Opening Trainer"
        };
        await window.ShowDialog(this);
    }

    private async void OpenSectionWindow(string title, IEnumerable<Control> content)
    {
        Window window = new()
        {
            Title = title,
            Width = 1320,
            Height = 900,
            MinWidth = 960,
            MinHeight = 700,
            Background = Brush.Parse("#23313B"),
            Content = new Border
            {
                Padding = new Thickness(18),
                Child = new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            CreateSectionCard(title, content)
                        }
                    }
                }
            }
        };

        await window.ShowDialog(this);
    }

    private static Border CreateInsightCard(string label, string value, string? detail = null)
    {
        Border card = new()
        {
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        StackPanel panel = new() { Spacing = 4 };
        panel.Children.Add(CreateBodyText(label, "#9EB5C5"));
        panel.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(detail))
        {
            panel.Children.Add(CreateBodyText(detail, "#D7E2EA"));
        }

        card.Child = panel;
        return card;
    }

    protected override void OnClosed(EventArgs e)
    {
        profileSessionTracker.TrackClosed("window_closed");
        base.OnClosed(e);
    }

    private bool IsClosed()
    {
        return VisualRoot is null || !IsVisible;
    }
}
