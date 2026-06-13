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
        return Plural(count, singularKey, pluralKey, pluralKey);
    }

    public static string Plural(int count, string singularKey, string fewKey, string manyKey)
    {
        string selectedKey = SelectPluralForm(count) switch
        {
            PluralForm.One => singularKey,
            PluralForm.Few => fewKey,
            _ => manyKey
        };

        return Format(selectedKey, count);
    }

    private static PluralForm SelectPluralForm(int count)
    {
        int absolute = Math.Abs(count);
        if (CurrentLanguage.Language == ApplicationLanguage.Polish)
        {
            int mod10 = absolute % 10;
            int mod100 = absolute % 100;
            if (absolute == 1)
            {
                return PluralForm.One;
            }

            if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14)
            {
                return PluralForm.Few;
            }

            return PluralForm.Many;
        }

        return absolute == 1 ? PluralForm.One : PluralForm.Many;
    }

    private enum PluralForm
    {
        One,
        Few,
        Many
    }
}
