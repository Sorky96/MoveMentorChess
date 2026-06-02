using System;
using System.Collections.Generic;

namespace MoveMentorChess.Tracking;

public sealed class TrackingTemplateBank
{
    private readonly Dictionary<string, List<float[]>> templates = new(StringComparer.Ordinal);

    public int Count => templates.Count;

    public void Add(string key, float[] vector, int maxVariants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(vector);

        if (maxVariants <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxVariants), maxVariants, "Template variant limit must be positive.");
        }

        if (!templates.TryGetValue(key, out List<float[]>? variants))
        {
            variants = new List<float[]>();
            templates[key] = variants;
        }

        variants.Add(vector);
        while (variants.Count > maxVariants)
        {
            variants.RemoveAt(0);
        }
    }

    public IEnumerable<(string Key, IReadOnlyList<float[]> Variants)> Enumerate()
    {
        foreach ((string key, List<float[]> variants) in templates)
        {
            yield return (key, variants.ToArray());
        }
    }
}
