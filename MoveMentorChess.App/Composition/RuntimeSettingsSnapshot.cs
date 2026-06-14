using MoveMentorChess.Analysis;

namespace MoveMentorChess.App.Composition;

internal sealed record RuntimeSettingsSnapshot(
    LlamaGpuSettings LlamaGpuSettings,
    StockfishSettings StockfishSettings,
    ApplicationSettings ApplicationSettings);
