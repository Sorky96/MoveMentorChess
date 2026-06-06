using System.Globalization;

namespace MoveMentorChess.Localization;

public sealed record LanguageOption(
    ApplicationLanguage Language,
    string CultureName,
    string EnglishName,
    string NativeName)
{
    public CultureInfo Culture => CultureInfo.GetCultureInfo(CultureName);

    public override string ToString() => NativeName;
}
