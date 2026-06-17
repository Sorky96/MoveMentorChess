using MoveMentorChess.Analysis;
using MoveMentorChess.App.Composition;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class DefaultMainWindowEngineSessionTests
{
    [Fact]
    public void ReloadReturnsUnavailableMessageWhenResolverThrows()
    {
        DefaultMainWindowEngineSession session = new(
            new ThrowingStockfishPathResolver(),
            () => StockfishSettings.Default);

        string message = session.Reload();

        Assert.False(session.IsAvailable);
        Assert.Contains("analysis engine is unavailable", message, StringComparison.Ordinal);
        Assert.Contains("resolver failed", message, StringComparison.Ordinal);
    }

    private sealed class ThrowingStockfishPathResolver : IStockfishPathResolver
    {
        public string? Resolve()
            => throw new InvalidOperationException("resolver failed");
    }
}
