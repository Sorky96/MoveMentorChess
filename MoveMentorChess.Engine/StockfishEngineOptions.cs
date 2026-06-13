namespace MoveMentorChess.Engine;

public sealed record StockfishEngineOptions(int Threads, int HashMb)
{
    public static StockfishEngineOptions Default { get; } = new(
        Threads: Math.Max(1, Environment.ProcessorCount - 1),
        HashMb: 256);
}
