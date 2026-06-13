using System.Globalization;

namespace MoveMentorChess.Localization;

public sealed class LanguageOption
{
    private readonly CultureInfo culture;

    public LanguageOption(
        ApplicationLanguage language,
        string cultureName,
        string englishName,
        string nativeName)
    {
        Language = language;
        CultureName = string.IsNullOrWhiteSpace(cultureName)
            ? throw new ArgumentException("Culture name is required.", nameof(cultureName))
            : cultureName;
        EnglishName = string.IsNullOrWhiteSpace(englishName)
            ? throw new ArgumentException("English name is required.", nameof(englishName))
            : englishName;
        NativeName = string.IsNullOrWhiteSpace(nativeName)
            ? throw new ArgumentException("Native name is required.", nameof(nativeName))
            : nativeName;

        try
        {
            culture = CultureInfo.GetCultureInfo(CultureName);
        }
        catch (CultureNotFoundException ex)
        {
            throw new ArgumentException($"Unsupported culture '{CultureName}'.", nameof(cultureName), ex);
        }
    }

    public ApplicationLanguage Language { get; }

    public string CultureName { get; }

    public string EnglishName { get; }

    public string NativeName { get; }

    public CultureInfo Culture => culture;

    public override string ToString() => NativeName;
}
