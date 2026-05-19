namespace MoveMentorChess.Domain;

public static class OpeningLineCatalogBuilder
{
    private const char CompositeKeySeparator = '|';

    public static OpeningLineCatalogItem CreateItem(
        string eco,
        string openingName,
        string variationName,
        RepertoireSide repertoireSide,
        OpeningPositionKey rootPositionKey,
        string rootFen,
        int bookGameCount,
        int bookBranchCount)
    {
        string displayName = BuildDisplayName(eco, openingName, variationName);
        return new OpeningLineCatalogItem(
            new OpeningKey(BuildOpeningKey(eco, openingName)),
            new OpeningLineKey(BuildOpeningLineKey(
                eco,
                openingName,
                variationName,
                repertoireSide,
                rootPositionKey.Value)),
            repertoireSide,
            eco,
            openingName,
            variationName,
            displayName,
            rootPositionKey,
            rootFen,
            bookGameCount,
            bookBranchCount);
    }

    public static string BuildDisplayName(string eco, string openingName, string variationName)
    {
        string opening = string.IsNullOrWhiteSpace(openingName) ? OpeningCatalog.GetName(eco) : openingName;
        return string.IsNullOrWhiteSpace(variationName)
            ? $"{opening} ({eco})"
            : $"{opening}: {variationName} ({eco})";
    }

    private static string BuildOpeningKey(string eco, string openingName)
    {
        return $"{SanitizeKeyPart(eco)}{CompositeKeySeparator}{SanitizeKeyPart(openingName)}";
    }

    private static string BuildOpeningLineKey(
        string eco,
        string openingName,
        string variationName,
        RepertoireSide side,
        string positionKey)
    {
        return string.Join(
            CompositeKeySeparator,
            SanitizeKeyPart(eco),
            SanitizeKeyPart(openingName),
            SanitizeKeyPart(variationName),
            side.ToString(),
            SanitizeKeyPart(positionKey));
    }

    private static string SanitizeKeyPart(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);
    }
}
