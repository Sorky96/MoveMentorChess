using System.Globalization;
using System.Resources;

namespace MoveMentorChess.Localization;

public static class Localizer
{
    private static readonly ResourceManager ResourceManager = new(
        "MoveMentorChess.Localization.Resources.Strings",
        typeof(Localizer).Assembly);

    private static readonly AsyncLocal<CultureInfo?> CurrentAsyncCulture = new();
    private static CultureInfo applicationCulture = LanguageCatalog.English.Culture;

    public static CultureInfo CurrentCulture
    {
        get => CurrentAsyncCulture.Value ?? applicationCulture;
        private set => CurrentAsyncCulture.Value = value;
    }

    public static LanguageOption CurrentLanguage => LanguageCatalog.Resolve(CurrentCulture.Name);

    public static void UseLanguage(LanguageOption language)
    {
        ArgumentNullException.ThrowIfNull(language);
        CurrentCulture = language.Culture;
    }

    public static void UseCulture(string? cultureName)
    {
        UseLanguage(LanguageCatalog.Resolve(cultureName));
    }

    public static void UseApplicationLanguage(LanguageOption language)
    {
        ArgumentNullException.ThrowIfNull(language);
        applicationCulture = language.Culture;
        CurrentAsyncCulture.Value = null;
    }

    public static void UseApplicationCulture(string? cultureName)
    {
        UseApplicationLanguage(LanguageCatalog.Resolve(cultureName));
    }

    public static string Text(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ResourceManager.GetString(key, CurrentCulture)
            ?? ResourceManager.GetString(key, LanguageCatalog.English.Culture)
            ?? key;
    }

    public static string Format(string key, params object?[] args)
    {
        return string.Format(CurrentCulture, Text(key), args);
    }

    public static string Plural(int count, string singularKey, string pluralKey)
    {
        return count == 1
            ? Format(singularKey, count)
            : Format(pluralKey, count);
    }
}
