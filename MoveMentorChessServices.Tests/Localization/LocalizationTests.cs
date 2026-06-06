using System.Globalization;
using System.Collections;
using System.Resources;
using MoveMentorChess.Analysis;
using MoveMentorChess.Localization;
using Xunit;

namespace MoveMentorChessServices.Tests.Localization;

public sealed class LocalizationTests
{
    [Fact]
    public void LanguageCatalog_ContainsRequestedLanguages()
    {
        string[] cultures = LanguageCatalog.SupportedLanguages.Select(language => language.CultureName).ToArray();

        Assert.Equal(["en", "zh-CN", "pt-BR", "pl", "de"], cultures);
    }

    [Fact]
    public void LanguageOptions_CacheCultureAndValidateCultureNames()
    {
        LanguageOption language = LanguageCatalog.Resolve("pt-BR");

        Assert.Same(language.Culture, language.Culture);
        Assert.Throws<ArgumentException>(() => new LanguageOption(
            ApplicationLanguage.English,
            "xx-INVALID",
            "Invalid",
            "Invalid"));
    }

    [Fact]
    public void LanguageCatalog_ResolvesSystemLanguageWithEnglishFallback()
    {
        Assert.Equal("pl", LanguageCatalog.ResolveDefault(CultureInfo.GetCultureInfo("pl-PL")).CultureName);
        Assert.Equal("zh-CN", LanguageCatalog.ResolveDefault(CultureInfo.GetCultureInfo("zh-Hant")).CultureName);
        Assert.Equal("pt-BR", LanguageCatalog.ResolveDefault(CultureInfo.GetCultureInfo("pt-PT")).CultureName);
        Assert.Equal("en", LanguageCatalog.ResolveDefault(CultureInfo.GetCultureInfo("fr-FR")).CultureName);
    }

    [Fact]
    public void Localizer_FallsBackToEnglishForUnsupportedCulture()
    {
        Localizer.UseCulture("xx-XX");

        Assert.Equal("Opening review", Localizer.Text(LocalizedStrings.TrainingBlockOpeningReview));
    }

    [Fact]
    public void ResourceFiles_HaveMatchingKeySets()
    {
        string resourceDirectory = FindResourceDirectory();
        string[] cultures = ["de", "pl", "pt-BR", "zh-CN"];
        string[] neutralKeys = ReadResourceKeys(Path.Combine(resourceDirectory, "Strings.resx"));

        foreach (string culture in cultures)
        {
            string[] localizedKeys = ReadResourceKeys(Path.Combine(resourceDirectory, $"Strings.{culture}.resx"));
            Assert.Empty(neutralKeys.Except(localizedKeys, StringComparer.Ordinal));
            Assert.Empty(localizedKeys.Except(neutralKeys, StringComparer.Ordinal));
        }
    }

    [Fact]
    public void Localizer_UsesPolishFewPluralForm()
    {
        Localizer.UseCulture("pl");

        Assert.Equal("2 błędy", Localizer.Plural(
            2,
            LocalizedStrings.CountOneMistake,
            LocalizedStrings.CountFewMistakes,
            LocalizedStrings.CountManyMistakes));
        Assert.Equal("5 błędów", Localizer.Plural(
            5,
            LocalizedStrings.CountOneMistake,
            LocalizedStrings.CountFewMistakes,
            LocalizedStrings.CountManyMistakes));
    }

    [Fact]
    public void AdvicePrompt_IncludesSelectedOutputLanguageAndKeepsJsonKeys()
    {
        Localizer.UseCulture("pl");
        LocalModelAdviceRequest request = new(
            CreateReplayPly(),
            MoveQualityBucket.Mistake,
            new MistakeTag("opening_principles", 0.9, []),
            "e2e4",
            120,
            ExplanationLevel.Intermediate,
            null,
            string.Empty);

        string prompt = AdvicePromptFormatter.BuildPrompt(request);

        Assert.Contains("po polsku", prompt, StringComparison.Ordinal);
        Assert.Contains("short_text", prompt, StringComparison.Ordinal);
        Assert.Contains("detailed_text", prompt, StringComparison.Ordinal);
        Assert.Contains("training_hint", prompt, StringComparison.Ordinal);
        Assert.Contains("referenced_best_move_uci", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateAdviceGenerator_UsesLocalizedHeadingsAndPatternNames()
    {
        Localizer.UseCulture("de");
        TemplateAdviceGenerator generator = new();

        MoveExplanation explanation = generator.Generate(
            CreateReplayPly(),
            MoveQualityBucket.Mistake,
            new MistakeTag("hanging_piece", 0.8, []),
            null,
            90);

        Assert.Contains("Was:", explanation.DetailedText, StringComparison.Ordinal);
        Assert.Contains("hängende Figur", explanation.ShortText, StringComparison.Ordinal);
    }

    private static ReplayPly CreateReplayPly()
    {
        return new ReplayPly(
            1,
            1,
            PlayerSide.White,
            "e4",
            "e4",
            "e2e4",
            "rn1qkbnr/ppp2ppp/3p4/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 0 4",
            "rn1qkbnr/ppp2ppp/3p4/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 0 4",
            "rn1qkbnr/ppp2ppp/3p4/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R",
            "rn1qkbnr/ppp2ppp/3p4/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R",
            GamePhase.Opening,
            "P",
            null,
            "e2",
            "e4",
            false,
            false,
            false);
    }

    private static string FindResourceDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "MoveMentorChess.Localization", "Resources");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find localization resources.");
    }

    private static string[] ReadResourceKeys(string path)
    {
        using ResXResourceReader reader = new(path);
        return reader
            .Cast<DictionaryEntry>()
            .Select(entry => entry.Key?.ToString() ?? string.Empty)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
