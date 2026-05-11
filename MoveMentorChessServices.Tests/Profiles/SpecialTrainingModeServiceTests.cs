using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class SpecialTrainingModeServiceTests
{
    [Fact]
    public void ListDefinitions_ContainsRoadmapModes()
    {
        SpecialTrainingModeService service = new();

        IReadOnlyList<SpecialTrainingModeDefinition> modes = service.ListDefinitions();

        Assert.Contains(modes, mode => mode.Kind == SpecialTrainingModeKind.FiveMinutePrep);
        Assert.Contains(modes, mode => mode.Kind == SpecialTrainingModeKind.OpponentPreparation);
        Assert.Contains(modes, mode => mode.Kind == SpecialTrainingModeKind.QuickBlackReview);
        Assert.Contains(modes, mode => mode.Kind == SpecialTrainingModeKind.RepairWeakestPositions);
    }

    [Fact]
    public void BuildOptions_CarriesModeLimitsAndPreset()
    {
        SpecialTrainingModeService service = new();
        SpecialTrainingModeDefinition mode = service.ListDefinitions()
            .First(item => item.Kind == SpecialTrainingModeKind.RepairWeakestPositions);

        OpeningTrainingSessionOptions options = service.BuildOptions(mode);

        Assert.Equal(SpecialTrainingModeKind.RepairWeakestPositions, options.SpecialMode);
        Assert.Equal(mode.TimeLimitMinutes, options.TimeLimitMinutes);
        Assert.Equal(mode.MaxPositions, options.MaxPositions);
        Assert.Contains(OpeningTrainingMode.MistakeRepair, options.Modes!);
    }
}
