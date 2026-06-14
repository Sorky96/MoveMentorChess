using Avalonia.Interactivity;

namespace MoveMentorChess.App.Controls;

public sealed class BoardSquarePressedEventArgs : RoutedEventArgs
{
    public BoardSquarePressedEventArgs(RoutedEvent routedEvent, string square)
        : base(routedEvent)
    {
        Square = square;
    }

    public string Square { get; }
}
