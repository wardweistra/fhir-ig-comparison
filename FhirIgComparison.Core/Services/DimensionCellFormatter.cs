using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public sealed class DimensionCellFormatter
{
    public IReadOnlyList<string> FormatAll(ComparisonDimension dimension, IReadOnlyList<SnapshotElement?> elements) =>
        dimension switch
        {
            ComparisonDimension.Cardinality => elements.Select(FormatCardinality).ToList(),
            ComparisonDimension.MustSupport => elements.Select(FormatMustSupport).ToList(),
            ComparisonDimension.Binding => elements.Select(FormatBinding).ToList(),
            ComparisonDimension.Type => elements.Select(FormatType).ToList(),
            ComparisonDimension.FixedPattern => elements.Select(FormatFixedPattern).ToList(),
            ComparisonDimension.Extensions => elements.Select(FormatExtension).ToList(),
            ComparisonDimension.Slicing => elements.Select(FormatSlicing).ToList(),
            ComparisonDimension.Constraints => elements.Select(FormatConstraints).ToList(),
            ComparisonDimension.Mappings => elements.Select(FormatMappings).ToList(),
            _ => elements.Select(_ => "—").ToList()
        };

    public static string FormatCardinality(SnapshotElement? element)
    {
        if (element is null)
            return "—";
        var max = string.IsNullOrEmpty(element.Max) ? "*" : element.Max;
        return $"{element.Min ?? 0}..{max}";
    }

    public static string FormatMustSupport(SnapshotElement? element) =>
        element?.MustSupport == true ? "MS" : "—";

    public static string FormatBinding(SnapshotElement? element)
    {
        if (string.IsNullOrWhiteSpace(element?.BindingValueSet))
            return "—";
        var strength = element.BindingStrength ?? "—";
        return $"{element.BindingValueSet} @ {strength}";
    }

    public static string FormatType(SnapshotElement? element)
    {
        if (element?.Types is null || element.Types.Count == 0)
            return "—";

        var parts = element.Types.Select(t =>
        {
            var code = t.Code ?? "?";
            if (t.Profiles.Count > 0)
                return $"{code}({string.Join("|", t.Profiles)})";
            if (t.TargetProfiles.Count > 0)
                return $"{code}→{string.Join("|", t.TargetProfiles)}";
            return code;
        });
        return string.Join(", ", parts);
    }

    public static string FormatFixedPattern(SnapshotElement? element) =>
        element?.FixedPatternDisplay ?? "—";

    public static string FormatExtension(SnapshotElement? element)
    {
        if (element is null)
            return "—";

        var max = string.IsNullOrEmpty(element.Max) ? "*" : element.Max;
        var cardinality = $"({element.Min ?? 0}..{max})";

        if (element.ExtensionUrls.Count > 0)
            return $"{string.Join(" | ", element.ExtensionUrls)} {cardinality}";

        var label = element.Path.Contains("extension", StringComparison.OrdinalIgnoreCase)
            ? element.Path
            : element.ElementId ?? element.Path;
        return $"{label} {cardinality}";
    }

    public static string FormatSlicing(SnapshotElement? element)
    {
        if (string.IsNullOrWhiteSpace(element?.SlicingKey))
            return "—";

        var rules = element.SlicingRules ?? "—";
        return $"{element.SlicingKey} [{rules}]";
    }

    public static string FormatConstraints(SnapshotElement? element) =>
        element?.ConstraintsDisplay ?? "—";

    public static string FormatMappings(SnapshotElement? element) =>
        element?.MappingsDisplay ?? "—";
}
