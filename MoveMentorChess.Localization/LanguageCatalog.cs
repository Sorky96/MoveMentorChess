using System.Globalization;

namespace MoveMentorChess.Localization;

public static class LanguageCatalog
{
    private static readonly LanguageOption[] Options =
    [
        new(ApplicationLanguage.English, "en", "English", "English"),
        new(ApplicationLanguage.ChineseSimplified, "zh-CN", "Chinese (Simplified)", "简体中文"),
        new(ApplicationLanguage.PortugueseBrazil, "pt-BR", "Portuguese (Brazil)", "Português (Brasil)"),
        new(ApplicationLanguage.Polish, "pl", "Polish", "Polski"),
        new(ApplicationLanguage.German, "de", "German", "Deutsch")
    ];

    public static IReadOnlyList<LanguageOption> SupportedLanguages => Options;

    public static LanguageOption English => Options[0];

    public static LanguageOption Resolve(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return ResolveDefault(CultureInfo.CurrentUICulture);
        }

        LanguageOption? exact = Options.FirstOrDefault(option =>
            string.Equals(option.CultureName, cultureName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        try
        {
            return ResolveDefault(CultureInfo.GetCultureInfo(cultureName.Trim()));
        }
        catch (CultureNotFoundException)
        {
            return English;
        }
    }

    public static LanguageOption ResolveDefault(CultureInfo systemCulture)
    {
        ArgumentNullException.ThrowIfNull(systemCulture);

        LanguageOption? exact = Options.FirstOrDefault(option =>
            string.Equals(option.CultureName, systemCulture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        string twoLetterName = systemCulture.TwoLetterISOLanguageName;
        return twoLetterName switch
        {
            "zh" => Options.First(option => option.Language == ApplicationLanguage.ChineseSimplified),
            "pt" => Options.First(option => option.Language == ApplicationLanguage.PortugueseBrazil),
            "pl" => Options.First(option => option.Language == ApplicationLanguage.Polish),
            "de" => Options.First(option => option.Language == ApplicationLanguage.German),
            "en" => English,
            _ => English
        };
    }
}
