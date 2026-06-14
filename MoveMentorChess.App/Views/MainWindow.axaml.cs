using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MoveMentorChess.App.Composition;
using MoveMentorChess.App.Controls;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Localization;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.Views;

public partial class MainWindow : Window
{
    private readonly IAnalysisWindowFactory analysisWindowFactory;
    private readonly IProfilesWindowFactory profilesWindowFactory;
    private readonly IMainWindowDialogDataService dialogDataService;
    private readonly ISettingsWindowFactory settingsWindowFactory;

    public MainWindow()
        : this(
            new AnalysisWindowFactory(),
            new ProfilesWindowFactory(() => null),
            () => null)
    {
    }

    public MainWindow(
        IAnalysisWindowFactory analysisWindowFactory,
        IProfilesWindowFactory profilesWindowFactory,
        Func<IAnalysisStore?> analysisStoreProvider)
        : this(
            analysisWindowFactory,
            profilesWindowFactory,
            new DefaultMainWindowDialogDataService(analysisStoreProvider),
            new SettingsWindowFactory(new DefaultSettingsWorkflow()))
    {
    }

    internal MainWindow(
        IAnalysisWindowFactory analysisWindowFactory,
        IProfilesWindowFactory profilesWindowFactory,
        IMainWindowDialogDataService dialogDataService,
        ISettingsWindowFactory settingsWindowFactory)
    {
        this.analysisWindowFactory = analysisWindowFactory ?? throw new ArgumentNullException(nameof(analysisWindowFactory));
        this.profilesWindowFactory = profilesWindowFactory ?? throw new ArgumentNullException(nameof(profilesWindowFactory));
        this.dialogDataService = dialogDataService ?? throw new ArgumentNullException(nameof(dialogDataService));
        this.settingsWindowFactory = settingsWindowFactory ?? throw new ArgumentNullException(nameof(settingsWindowFactory));
        InitializeComponent();
        ApplyLocalizedText();
        Opened += (_, _) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SetPromotionMoveSelector(ShowPromotionDialogAsync);
            }
        };
        Closing += (_, _) =>
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };
    }

    private async void BoardView_OnSquarePressed(object? sender, BoardSquarePressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.HandleSquareClickAsync(e.Square);
        }

        BoardView.InvalidateVisual();
    }

    private async void PgnImportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        PgnImportWindow dialog = new();
        bool? result = await dialog.ShowDialog<bool?>(this);
        if (result == true && !string.IsNullOrWhiteSpace(dialog.PgnText))
        {
            viewModel.ImportPgn(dialog.PgnText);
        }
    }

    private async void LoadSavedButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        ISavedLibraryDataService savedLibrary = dialogDataService.SavedLibrary;
        if (!savedLibrary.IsAvailable)
        {
            return;
        }

        SavedGamesWindow dialog = new(savedLibrary);
        bool? result = await dialog.ShowDialog<bool?>(this);
        if (result == true && dialog.SelectedGame is not null)
        {
            viewModel.LoadImportedGame(dialog.SelectedGame);
        }
    }

    private async void LoadPgnFileButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Localizer.Text(LocalizedStrings.MainDialogLoadPgnFile),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Localizer.Text(LocalizedStrings.MainPgnFiles))
                {
                    Patterns = ["*.pgn"],
                    MimeTypes = ["application/x-chess-pgn", "text/plain"]
                },
                FilePickerFileTypes.TextPlain,
                FilePickerFileTypes.All
            ]
        });

        IStorageFile? file = files.Count > 0 ? files[0] : null;
        if (file is null)
        {
            return;
        }

        string pgnText;
        await using (Stream stream = await file.OpenReadAsync())
        using (StreamReader reader = new(stream))
        {
            pgnText = await reader.ReadToEndAsync();
        }

        PgnBatchParseResult parseResult;
        try
        {
            parseResult = await Task.Run(() => PgnGameParser.ParseMany(pgnText));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await ShowInfoDialogAsync(
                Localizer.Text(LocalizedStrings.MainDialogLoadPgnFile),
                $"{Localizer.Text(LocalizedStrings.MainDialogCouldNotReadPgnFile)}\n{ex.Message}");
            return;
        }

        PgnFileImportResult importResult = viewModel.ImportPgnGames(parseResult);
        if (importResult.ImportedGames == 0)
        {
            await ShowInfoDialogAsync(
                Localizer.Text(LocalizedStrings.MainDialogLoadPgnFile),
                Localizer.Text(LocalizedStrings.MainDialogNoReplayableGames));
            return;
        }

        if (!viewModel.HasAnalysisEngine())
        {
            await ShowInfoDialogAsync(
                Localizer.Text(LocalizedStrings.MainDialogLoadPgnFile),
                Localizer.Format(LocalizedStrings.MainDialogLoadedGamesEngineUnavailable, importResult.ImportedGames));
            return;
        }

        string skippedText = importResult.SkippedGames > 0
            ? $" {Localizer.Format(LocalizedStrings.MainDialogSkippedGames, importResult.SkippedGames)}"
            : string.Empty;
        string? primaryPlayer = MainWindowViewModel.DetectPrimaryPlayer(importResult.Games);
        string analysisTargetText = string.IsNullOrWhiteSpace(primaryPlayer)
            ? Localizer.Text(LocalizedStrings.MainDialogNoRecurringPlayer)
            : Localizer.Format(LocalizedStrings.MainDialogDetectedPlayer, primaryPlayer);
        bool analyze = await ShowConfirmationDialogAsync(
            Localizer.Text(LocalizedStrings.MainDialogAnalyzeImportedGamesTitle),
            $"{Localizer.Format(LocalizedStrings.MainDialogLoadedGames, importResult.ImportedGames)}{skippedText}\n\n{analysisTargetText}\n\n{Localizer.Text(LocalizedStrings.MainDialogAnalyzeNow)}",
            Localizer.Text(LocalizedStrings.MainDialogAnalyze),
            Localizer.Text(LocalizedStrings.MainDialogLater));

        if (analyze)
        {
            BulkPgnAnalysisResult analysisResult = await viewModel.AnalyzeImportedGamesAsync(importResult.Games);
            await ShowInfoDialogAsync(Localizer.Text(LocalizedStrings.MainDialogPgnAnalysisFinished), BuildBulkAnalysisSummary(analysisResult));
        }
    }

    private async void ProfilesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        ProfilesWindow dialog = profilesWindowFactory.Create(new ProfilesWindowRequest(
            viewModel.NavigateToProfileExampleAsync,
            viewModel.NavigateToOpeningExampleAsync,
            viewModel.NavigateToOpeningPositionAsync));
        await dialog.ShowDialog(this);
    }

    private async void AnalyzeImportedButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        AnalysisWindowRequest? request = viewModel.CreateAnalysisWindowRequest();
        if (request is null)
        {
            return;
        }

        AnalysisWindow dialog = analysisWindowFactory.Create(request);
        await dialog.ShowDialog(this);
        if (dialog.CurrentResult is not null)
        {
            viewModel.LoadAnalysisResult(dialog.CurrentResult);
        }
    }

    private async void SavedAnalysesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        ISavedLibraryDataService savedLibrary = dialogDataService.SavedLibrary;
        if (!savedLibrary.IsAvailable)
        {
            return;
        }

        SavedAnalysesWindow dialog = new(savedLibrary, canOpenAnalysis: true);
        bool? result = await dialog.ShowDialog<bool?>(this);
        if (result != true || dialog.SelectedResult is null)
        {
            return;
        }

        viewModel.LoadAnalysisResult(dialog.SelectedResult);

        if (dialog.RequestedAction == SavedAnalysisAction.OpenAnalysis)
        {
            AnalysisWindowRequest? request = viewModel.CreateAnalysisWindowRequest();
            if (request is not null)
            {
                AnalysisWindow analysisWindow = analysisWindowFactory.Create(request);
                await analysisWindow.ShowDialog(this);
                if (analysisWindow.CurrentResult is not null)
                {
                    viewModel.LoadAnalysisResult(analysisWindow.CurrentResult);
                }
            }
        }
    }

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        SettingsWindow dialog = settingsWindowFactory.Create();
        bool? saved = await dialog.ShowDialog<bool?>(this);
        if (saved == true && DataContext is MainWindowViewModel viewModel)
        {
            ApplyLocalizedText();
            viewModel.ReloadEngineSettings();
        }
    }

    private async void OpeningTrainerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!dialogDataService.TryCreateOpeningTrainerViewModel(out OpeningTrainerWindowViewModel? viewModel) || viewModel is null)
        {
            return;
        }

        OpeningTrainerWindow dialog = new(viewModel);
        await dialog.ShowDialog(this);
    }

    private async void OpeningCoverageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!dialogDataService.TryCreateOpeningCoverageViewModel(out OpeningCoverageWindowViewModel? viewModel) || viewModel is null)
        {
            return;
        }

        OpeningCoverageWindow coverageWindow = new(viewModel);
        bool? result = await coverageWindow.ShowDialog<bool?>(this);
        if (result == true && coverageWindow.SelectedLine is not null)
        {
            if (!dialogDataService.TryCreateOpeningTrainerViewModel(out OpeningTrainerWindowViewModel? trainerViewModel) || trainerViewModel is null)
            {
                return;
            }

            trainerViewModel.OpenLineFromCoverage(coverageWindow.SelectedLine);
            OpeningTrainerWindow trainerWindow = new(trainerViewModel);
            await trainerWindow.ShowDialog(this);
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyLocalizedText()
    {
        RotateBoardButton.Content = Localizer.Text(LocalizedStrings.MainRotateBoard);
        UndoButton.Content = Localizer.Text(LocalizedStrings.MainUndo);
        ConfigureEngineButton.Content = Localizer.Text(LocalizedStrings.MainConfigureEngine);
        HeroPastePgnButton.Content = Localizer.Text(LocalizedStrings.MainPastePgn);
        HeroAnalyzeGameButton.Content = Localizer.Text(LocalizedStrings.MainAnalyzeGame);
        PastePgnButton.Content = Localizer.Text(LocalizedStrings.MainPastePgn);
        LoadPgnFileButton.Content = Localizer.Text(LocalizedStrings.MainLoadPgnFile);
        ApplyNextButton.Content = Localizer.Text(LocalizedStrings.MainApplyNext);
        ApplySelectedButton.Content = Localizer.Text(LocalizedStrings.MainApplySelected);
        AnalyzeImportedButton.Content = Localizer.Text(LocalizedStrings.MainAnalyzeImported);
        PlayerCoachButton.Content = Localizer.Text(LocalizedStrings.MainPlayerCoach);
        SavedAnalysesButton.Content = Localizer.Text(LocalizedStrings.MainSavedAnalyses);
        LoadSavedButton.Content = Localizer.Text(LocalizedStrings.MainLoadSaved);
        OpeningTrainerButton.Content = Localizer.Text(LocalizedStrings.MainOpeningTrainer);
        SettingsButton.Content = Localizer.Text(LocalizedStrings.MainSettings);
        OpeningCoverageButton.Content = Localizer.Text(LocalizedStrings.MainOpeningCoverage);
        CloseAppButton.Content = Localizer.Text(LocalizedStrings.MainCloseApp);
    }

    private async Task<LegalMoveInfo?> ShowPromotionDialogAsync(IReadOnlyList<LegalMoveInfo> moves)
    {
        PromotionWindow dialog = new(moves);
        bool? result = await dialog.ShowDialog<bool?>(this);
        return result == true ? dialog.SelectedMove : null;
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        await ShowConfirmationDialogAsync(title, message, Localizer.Text(LocalizedStrings.MainDialogOk), null);
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string message, string acceptText, string? cancelText)
    {
        Window dialog = new()
        {
            Title = title,
            Width = 520,
            Height = 300,
            MinWidth = 460,
            MinHeight = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Brush.Parse("#101820")
        };

        TextBlock messageBlock = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush.Parse("#D7E2EA"),
            FontSize = 15
        };
        ScrollViewer messageScroller = new()
        {
            Content = messageBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        if (!string.IsNullOrWhiteSpace(cancelText))
        {
            Button cancelButton = new()
            {
                Content = cancelText,
                Width = 100
            };
            cancelButton.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(cancelButton);
        }

        Button acceptButton = new()
        {
            Content = acceptText,
            Width = 100
        };
        acceptButton.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(acceptButton);

        Border messagePanel = new()
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(18),
            Background = Brush.Parse("#182733"),
            Child = messageScroller
        };

        Grid content = new()
        {
            Margin = new Thickness(18),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                messagePanel,
                buttons
            }
        };

        buttons.Margin = new Thickness(0, 14, 0, 0);
        Grid.SetRow(buttons, 1);
        dialog.Content = content;

        bool? result = await dialog.ShowDialog<bool?>(this);
        return result == true;
    }

    private static string BuildBulkAnalysisSummary(BulkPgnAnalysisResult result)
    {
        string player = string.IsNullOrWhiteSpace(result.PrimaryPlayer)
            ? Localizer.Text(LocalizedStrings.MainBulkDetectedPlayerNone)
            : Localizer.Format(LocalizedStrings.MainBulkAnalyzedPlayer, result.PrimaryPlayer);
        string summary =
            $"{player}\n\n{Localizer.Format(LocalizedStrings.MainBulkNewAnalyses, result.AnalyzedGames)}\n{Localizer.Format(LocalizedStrings.MainBulkLoadedFromCache, result.CachedGames)}\n{Localizer.Format(LocalizedStrings.MainBulkSkipped, result.SkippedGames)}\n{Localizer.Format(LocalizedStrings.MainBulkFailed, result.FailedGames)}";

        if (result.FailureMessages.Count == 0)
        {
            return summary;
        }

        return summary + $"\n\n{Localizer.Text(LocalizedStrings.MainBulkFirstFailures)}\n" + string.Join("\n", result.FailureMessages);
    }
}
