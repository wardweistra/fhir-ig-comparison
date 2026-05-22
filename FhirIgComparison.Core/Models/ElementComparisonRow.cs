namespace FhirIgComparison.Core.Models;

public sealed class ElementComparisonRow
{
    public required string Path { get; init; }
    public string? SliceName { get; init; }
    public string? ExtensionUrl { get; init; }
    public required string ElementId { get; init; }
    public IReadOnlyList<SnapshotElement?> ElementsByIg { get; init; } = [];
    public IReadOnlyDictionary<ComparisonDimension, IReadOnlyList<string>> DisplayValuesByDimension { get; init; }
        = new Dictionary<ComparisonDimension, IReadOnlyList<string>>();
    public IReadOnlyDictionary<ComparisonDimension, CompatibilityResult> CompatibilityByDimension { get; init; }
        = new Dictionary<ComparisonDimension, CompatibilityResult>();

    public string DisplayPath
    {
        get
        {
            if (!string.IsNullOrEmpty(SliceName))
                return $"{Path}:{SliceName}";

            if (!string.IsNullOrEmpty(ExtensionUrl))
                return $"{Path} @ {TruncateUrl(ExtensionUrl)}";

            return Path;
        }
    }

    public bool IsExtensionRow =>
        Path.Contains(".extension", StringComparison.OrdinalIgnoreCase)
        || Path.EndsWith("extension", StringComparison.OrdinalIgnoreCase);

    public bool IsMustSupportInAny =>
        ElementsByIg.Any(e => e?.MustSupport == true);

    public bool HasDifference(ComparisonDimension dimension)
    {
        if (!DisplayValuesByDimension.TryGetValue(dimension, out var values))
            return false;

        var present = values.Where(v => v != "—").Distinct(StringComparer.Ordinal).ToList();
        return present.Count > 1;
    }

    public bool HasConflict(ComparisonDimension dimension) =>
        CompatibilityByDimension.TryGetValue(dimension, out var result)
        && result.Status == CompatibilityStatus.Conflict;

    public bool MatchesFilter(ComparisonRowFilter filter, ComparisonDimension activeDimension)
    {
        if (activeDimension == ComparisonDimension.Extensions && !IsExtensionRow)
            return false;

        return filter switch
        {
            ComparisonRowFilter.All => true,
            ComparisonRowFilter.DifferencesOnly => HasDifference(activeDimension),
            ComparisonRowFilter.ConflictsOnly => HasConflict(activeDimension),
            ComparisonRowFilter.MustSupportUnion => IsMustSupportInAny,
            _ => true
        };
    }

    private static string TruncateUrl(string url)
    {
        const int maxLength = 60;
        if (url.Length <= maxLength)
            return url;

        var tailLength = 24;
        return url[..(maxLength - tailLength - 1)] + "…" + url[^tailLength..];
    }
}
