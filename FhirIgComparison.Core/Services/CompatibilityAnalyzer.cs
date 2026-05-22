using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public sealed class CompatibilityAnalyzer
{
    private static readonly HashSet<string> SignificantBindingStrengths =
        new(StringComparer.OrdinalIgnoreCase) { "Required", "Extensible" };

    public IReadOnlyDictionary<ComparisonDimension, CompatibilityResult> Analyze(
        IReadOnlyList<SnapshotElement?> elements)
    {
        return new Dictionary<ComparisonDimension, CompatibilityResult>
        {
            [ComparisonDimension.Cardinality] = AnalyzeCardinality(elements),
            [ComparisonDimension.MustSupport] = AnalyzeMustSupport(elements),
            [ComparisonDimension.Binding] = AnalyzeBinding(elements),
            [ComparisonDimension.Type] = AnalyzeType(elements),
            [ComparisonDimension.FixedPattern] = AnalyzeFixedPattern(elements),
            [ComparisonDimension.Extensions] = AnalyzeExtensions(elements),
            [ComparisonDimension.Slicing] = AnalyzeSlicing(elements),
            [ComparisonDimension.Constraints] = AnalyzeConstraints(elements),
            [ComparisonDimension.Mappings] = AnalyzeMappings(elements)
        };
    }

    public static CompatibilityResult AnalyzeCardinality(IReadOnlyList<SnapshotElement?> elements)
    {
        var ranges = elements
            .Where(e => e is not null)
            .Select(e => ParseCardinalityRange(e!))
            .ToList();

        if (ranges.Count == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "—",
                Status = CompatibilityStatus.NotApplicable,
                Explanation = "Element absent in all profiles."
            };
        }

        var combinedMin = ranges.Max(r => r.Min);
        var combinedMax = ranges.Min(r => r.Max);
        var combinedValue = FormatRange(combinedMin, combinedMax);

        if (combinedMin > combinedMax)
        {
            return new CompatibilityResult
            {
                CombinedValue = combinedValue,
                Status = CompatibilityStatus.Conflict,
                Explanation = "No instance can satisfy all cardinality ranges (max of mins > min of maxes)."
            };
        }

        var allSame = ranges.DistinctBy(r => (r.Min, r.Max)).Count() == 1;
        return new CompatibilityResult
        {
            CombinedValue = combinedValue,
            Status = allSame ? CompatibilityStatus.Compatible : CompatibilityStatus.Warning,
            Explanation = allSame ? null : "Intersection is stricter than some individual profiles."
        };
    }

    public static CompatibilityResult AnalyzeMustSupport(IReadOnlyList<SnapshotElement?> elements)
    {
        var msCount = elements.Count(e => e?.MustSupport == true);
        if (msCount == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "—",
                Status = CompatibilityStatus.Compatible
            };
        }

        return new CompatibilityResult
        {
            CombinedValue = $"MS in {msCount} profile(s)",
            Status = CompatibilityStatus.Compatible,
            Explanation = "Must-support is additive across profiles."
        };
    }

    public static CompatibilityResult AnalyzeBinding(IReadOnlyList<SnapshotElement?> elements)
    {
        var bindings = elements
            .Where(e => !string.IsNullOrWhiteSpace(e?.BindingValueSet))
            .Select(e => (Url: e!.BindingValueSet!, Strength: e.BindingStrength ?? "Required"))
            .ToList();

        if (bindings.Count == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "—",
                Status = CompatibilityStatus.NotApplicable
            };
        }

        var significant = bindings
            .Where(b => SignificantBindingStrengths.Contains(b.Strength))
            .ToList();

        if (significant.Count == 0)
        {
            var urls = bindings.Select(b => b.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return new CompatibilityResult
            {
                CombinedValue = urls.Count == 1 ? urls[0] : "varies",
                Status = urls.Count <= 1 ? CompatibilityStatus.Compatible : CompatibilityStatus.Warning,
                Explanation = "Non-required bindings differ."
            };
        }

        var distinctUrls = significant.Select(b => b.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinctUrls.Count > 1)
        {
            return new CompatibilityResult
            {
                CombinedValue = "incompatible",
                Status = CompatibilityStatus.Conflict,
                Explanation = "Required/extensible bindings use different ValueSets (structural check only)."
            };
        }

        var strengths = significant.Select(b => b.Strength).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (strengths.Count > 1)
        {
            return new CompatibilityResult
            {
                CombinedValue = distinctUrls[0],
                Status = CompatibilityStatus.Warning,
                Explanation = "Same ValueSet with different binding strengths."
            };
        }

        return new CompatibilityResult
        {
            CombinedValue = distinctUrls[0],
            Status = CompatibilityStatus.Compatible
        };
    }

    public static CompatibilityResult AnalyzeType(IReadOnlyList<SnapshotElement?> elements)
    {
        var typeSets = elements
            .Where(e => e?.Types is { Count: > 0 })
            .Select(e => new HashSet<string>(
                e!.Types.Select(FormatTypeKey),
                StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (typeSets.Count == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "—",
                Status = CompatibilityStatus.NotApplicable
            };
        }

        var intersection = typeSets
            .Skip(1)
            .Aggregate(
                new HashSet<string>(typeSets[0], StringComparer.OrdinalIgnoreCase),
                (acc, set) =>
                {
                    acc.IntersectWith(set);
                    return acc;
                });

        if (intersection.Count == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "∅",
                Status = CompatibilityStatus.Conflict,
                Explanation = "No common allowed type across profiles."
            };
        }

        var allSame = typeSets.DistinctBy(s => string.Join("|", s.OrderBy(x => x))).Count() == 1;
        return new CompatibilityResult
        {
            CombinedValue = string.Join(", ", intersection.OrderBy(x => x)),
            Status = allSame ? CompatibilityStatus.Compatible : CompatibilityStatus.Warning,
            Explanation = allSame ? null : "Type intersection is narrower than some profiles."
        };
    }

    public static CompatibilityResult AnalyzeFixedPattern(IReadOnlyList<SnapshotElement?> elements)
    {
        var values = elements
            .Select(DimensionCellFormatter.FormatFixedPattern)
            .Where(v => v != "—")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (values.Count == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "—",
                Status = CompatibilityStatus.NotApplicable
            };
        }

        if (values.Count > 1)
        {
            return new CompatibilityResult
            {
                CombinedValue = "conflict",
                Status = CompatibilityStatus.Conflict,
                Explanation = "Profiles fix or pattern different values on this element."
            };
        }

        return new CompatibilityResult
        {
            CombinedValue = Truncate(values[0], 80),
            Status = CompatibilityStatus.Compatible
        };
    }

    public static CompatibilityResult AnalyzeExtensions(IReadOnlyList<SnapshotElement?> elements)
    {
        var distinctUrls = elements
            .Where(e => e is not null)
            .Select(e => ExtensionElementIdentity.GetPrimaryExtensionUrl(e!))
            .Where(u => u is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctUrls.Count > 1)
        {
            return new CompatibilityResult
            {
                CombinedValue = "conflict",
                Status = CompatibilityStatus.Conflict,
                Explanation = "Profiles constrain different extension definitions on this row."
            };
        }

        var requiredIn = elements
            .Select((e, i) => (Index: i, Element: e))
            .Where(x => x.Element is not null && (x.Element.Min ?? 0) >= 1)
            .Select(x => x.Index)
            .ToList();

        var absentIn = elements
            .Select((e, i) => (Index: i, Element: e))
            .Where(x => x.Element is null)
            .Select(x => x.Index)
            .ToList();

        if (requiredIn.Count > 0 && absentIn.Count > 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "gap",
                Status = CompatibilityStatus.Warning,
                Explanation = "Required extension in some profiles but absent in others."
            };
        }

        return new CompatibilityResult
        {
            CombinedValue = "additive",
            Status = CompatibilityStatus.Compatible,
            Explanation = "Extensions are generally additive."
        };
    }

    public static CompatibilityResult AnalyzeSlicing(IReadOnlyList<SnapshotElement?> elements)
    {
        var slicingKeys = elements
            .Where(e => !string.IsNullOrWhiteSpace(e?.SlicingKey))
            .Select(e => e!.SlicingKey!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (slicingKeys.Count == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "—",
                Status = CompatibilityStatus.NotApplicable
            };
        }

        if (slicingKeys.Count > 1)
        {
            return new CompatibilityResult
            {
                CombinedValue = "conflict",
                Status = CompatibilityStatus.Conflict,
                Explanation = "Profiles use different slicing discriminators or rules."
            };
        }

        var hasClosed = elements.Any(e =>
            string.Equals(e?.SlicingRules, "Closed", StringComparison.OrdinalIgnoreCase));
        var hasOpen = elements.Any(e =>
            string.Equals(e?.SlicingRules, "Open", StringComparison.OrdinalIgnoreCase));

        if (hasClosed && hasOpen)
        {
            return new CompatibilityResult
            {
                CombinedValue = slicingKeys[0],
                Status = CompatibilityStatus.Warning,
                Explanation = "Mixed open and closed slicing rules."
            };
        }

        return new CompatibilityResult
        {
            CombinedValue = slicingKeys[0],
            Status = CompatibilityStatus.Compatible
        };
    }

    public static CompatibilityResult AnalyzeConstraints(IReadOnlyList<SnapshotElement?> elements)
    {
        var profilesWithConstraints = elements.Count(e => e?.HasConstraints == true);
        if (profilesWithConstraints == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "—",
                Status = CompatibilityStatus.NotApplicable
            };
        }

        if (profilesWithConstraints > 1)
        {
            return new CompatibilityResult
            {
                CombinedValue = $"{profilesWithConstraints} profiles",
                Status = CompatibilityStatus.Warning,
                Explanation = "Multiple profiles add invariants — review manually."
            };
        }

        return new CompatibilityResult
        {
            CombinedValue = "1 profile",
            Status = CompatibilityStatus.Compatible
        };
    }

    public static CompatibilityResult AnalyzeMappings(IReadOnlyList<SnapshotElement?> elements)
    {
        var profilesWithMappings = elements.Count(e => e?.HasMappings == true);
        if (profilesWithMappings == 0)
        {
            return new CompatibilityResult
            {
                CombinedValue = "—",
                Status = CompatibilityStatus.NotApplicable,
                Explanation = "Informational only."
            };
        }

        return new CompatibilityResult
        {
            CombinedValue = $"{profilesWithMappings} profile(s)",
            Status = CompatibilityStatus.Compatible,
            Explanation = "Mappings are informational and do not affect validation."
        };
    }

    private static (int Min, int Max) ParseCardinalityRange(SnapshotElement element)
    {
        var min = element.Min ?? 0;
        var max = ParseMax(element.Max);
        return (min, max);
    }

    private static int ParseMax(string? max)
    {
        if (string.IsNullOrEmpty(max) || max == "*")
            return int.MaxValue;
        return int.TryParse(max, out var n) ? n : int.MaxValue;
    }

    private static string FormatRange(int min, int max) =>
        max == int.MaxValue ? $"{min}..*" : $"{min}..{max}";

    private static string FormatTypeKey(SnapshotElementType type)
    {
        var code = type.Code ?? "?";
        if (type.TargetProfiles.Count > 0)
            return $"{code}→{string.Join("|", type.TargetProfiles)}";
        if (type.Profiles.Count > 0)
            return $"{code}({string.Join("|", type.Profiles)})";
        return code;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
