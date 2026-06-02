using System.IO;

namespace MoveMentorChess.Analysis;

public sealed class StoreBackedPlayerMistakeProfileSource : IPlayerMistakeProfileSource
{
    private readonly Func<IAnalysisResultStore?> storeProvider;

    public StoreBackedPlayerMistakeProfileSource(Func<IAnalysisResultStore?> storeProvider)
    {
        this.storeProvider = storeProvider ?? throw new ArgumentNullException(nameof(storeProvider));
    }

    public PlayerMistakeProfile? TryBuild(string? playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        try
        {
            IAnalysisResultStore? store = storeProvider();
            if (store is null)
            {
                return null;
            }

            return PlayerMistakeProfileProvider.TryBuildFromStore(store, playerName.Trim());
        }
        catch (InvalidOperationException)
        {
            // Store not available (e.g., during tests or first run).
            return null;
        }
        catch (IOException)
        {
            // Store access issue.
            return null;
        }
    }
}
