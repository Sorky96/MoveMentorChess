using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

public sealed class TrackingTemplateBankTests
{
    [Fact]
    public void Add_StoresTemplateVariantsByKey()
    {
        TrackingTemplateBank bank = new();
        float[] vector = [0.1f, 0.2f];

        bank.Add("P|L", vector, maxVariants: 2);

        (string Key, IReadOnlyList<float[]> Variants) entry = Assert.Single(bank.Enumerate());
        Assert.Equal("P|L", entry.Key);
        Assert.Same(vector, Assert.Single(entry.Variants));
    }

    [Fact]
    public void Add_TrimsOldestVariantWhenLimitIsExceeded()
    {
        TrackingTemplateBank bank = new();
        float[] first = [0.1f];
        float[] second = [0.2f];
        float[] third = [0.3f];

        bank.Add("N|D", first, maxVariants: 2);
        bank.Add("N|D", second, maxVariants: 2);
        bank.Add("N|D", third, maxVariants: 2);

        (string _, IReadOnlyList<float[]> variants) = Assert.Single(bank.Enumerate());
        Assert.Equal([second, third], variants);
    }

    [Fact]
    public void Add_EnforcesSmallerLimitWhenMaxVariantsShrinks()
    {
        TrackingTemplateBank bank = new();
        float[] first = [0.1f];
        float[] second = [0.2f];
        float[] third = [0.3f];
        float[] fourth = [0.4f];

        bank.Add("N|D", first, maxVariants: 4);
        bank.Add("N|D", second, maxVariants: 4);
        bank.Add("N|D", third, maxVariants: 4);
        bank.Add("N|D", fourth, maxVariants: 2);

        (string _, IReadOnlyList<float[]> variants) = Assert.Single(bank.Enumerate());
        Assert.Equal([third, fourth], variants);
    }

    [Fact]
    public void Enumerate_DoesNotExposeMutableBackingList()
    {
        TrackingTemplateBank bank = new();
        float[] vector = [0.1f];

        bank.Add("P|L", vector, maxVariants: 2);

        (string _, IReadOnlyList<float[]> variants) = Assert.Single(bank.Enumerate());
        Assert.IsNotType<List<float[]>>(variants);
    }

    [Fact]
    public void Add_RejectsInvalidArguments()
    {
        TrackingTemplateBank bank = new();

        Assert.Throws<ArgumentException>(() => bank.Add("", [0.1f], maxVariants: 1));
        Assert.Throws<ArgumentNullException>(() => bank.Add("P|L", null!, maxVariants: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => bank.Add("P|L", [0.1f], maxVariants: 0));
    }
}
