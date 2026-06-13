using Avalonia.Markup.Xaml;
using MoveMentorChess.Localization;

namespace MoveMentorChess.App.Localization;

public sealed class LocalizeExtension(string key) : MarkupExtension
{
    public string Key { get; } = string.IsNullOrWhiteSpace(key)
        ? throw new ArgumentException("Localization key is required.", nameof(key))
        : key;

    public override object ProvideValue(IServiceProvider serviceProvider)
        => Localizer.Text(Key);
}
