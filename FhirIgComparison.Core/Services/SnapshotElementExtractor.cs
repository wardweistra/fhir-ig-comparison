extern alias stu3;
using FhirIgComparison.Core.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Stu3 = stu3::Hl7.Fhir.Model;
using Stu3Serialization = stu3::Hl7.Fhir.Serialization;

namespace FhirIgComparison.Core.Services;

public sealed class SnapshotElementExtractor
{
    private static readonly FhirJsonSerializer R4Serializer = new();
    private static readonly Stu3Serialization.FhirJsonSerializer Stu3Serializer = new();

    public IReadOnlyList<SnapshotElement> FromStructureDefinition(StructureDefinition sd)
    {
        var activeSlices = new Dictionary<string, string>(StringComparer.Ordinal);
        var results = new List<SnapshotElement>();

        foreach (var element in sd.Snapshot?.Element ?? [])
        {
            if (string.IsNullOrEmpty(element.Path))
                continue;

            if (!string.IsNullOrEmpty(element.SliceName))
                activeSlices[element.Path] = element.SliceName;

            var elementId = !string.IsNullOrWhiteSpace(element.ElementId)
                ? element.ElementId
                : ElementIdSynthesizer.FromPath(element.Path, activeSlices);

            results.Add(FromR4Element(element, elementId));
        }

        return results;
    }

    public IReadOnlyList<SnapshotElement> FromStructureDefinition(Stu3.StructureDefinition sd)
    {
        var activeSlices = new Dictionary<string, string>(StringComparer.Ordinal);
        var results = new List<SnapshotElement>();

        foreach (var element in sd.Snapshot?.Element ?? [])
        {
            if (string.IsNullOrEmpty(element.Path))
                continue;

            if (!string.IsNullOrEmpty(element.SliceName))
                activeSlices[element.Path] = element.SliceName;

            var elementId = !string.IsNullOrWhiteSpace(element.ElementId)
                ? element.ElementId
                : ElementIdSynthesizer.FromPath(element.Path, activeSlices);

            results.Add(FromStu3Element(element, elementId));
        }

        return results;
    }

    private static SnapshotElement FromR4Element(ElementDefinition element, string elementId) =>
        new()
        {
            Path = element.Path ?? "",
            SliceName = element.SliceName,
            ElementId = elementId,
            Min = element.Min,
            Max = element.Max,
            MustSupport = element.MustSupport,
            BindingValueSet = FhirUriFormatter.FormatBindingValueSet(element.Binding),
            BindingStrength = element.Binding?.Strength?.ToString(),
            Types = element.Type?.Select(FromR4Type).ToList() ?? [],
            FixedPatternDisplay = FormatR4FixedPattern(element),
            ExtensionUrls = ExtensionUrlExtractor.FromR4(element),
            SlicingKey = element.Slicing is not null ? FormatR4SlicingKey(element.Slicing) : null,
            SlicingRules = element.Slicing?.Rules?.ToString(),
            HasConstraints = element.Constraint is { Count: > 0 },
            HasMappings = element.Mapping is { Count: > 0 },
            ConstraintsDisplay = FormatR4Constraints(element),
            MappingsDisplay = FormatR4Mappings(element)
        };

    private static SnapshotElement FromStu3Element(Stu3.ElementDefinition element, string elementId) =>
        new()
        {
            Path = element.Path ?? "",
            SliceName = element.SliceName,
            ElementId = elementId,
            Min = element.Min,
            Max = element.Max,
            MustSupport = element.MustSupport,
            BindingValueSet = FhirUriFormatter.FormatBindingValueSet(element.Binding),
            BindingStrength = element.Binding?.Strength?.ToString(),
            Types = element.Type?.Select(FromStu3Type).ToList() ?? [],
            FixedPatternDisplay = FormatStu3FixedPattern(element),
            ExtensionUrls = ExtensionUrlExtractor.FromStu3(element),
            SlicingKey = element.Slicing is not null ? FormatStu3SlicingKey(element.Slicing) : null,
            SlicingRules = element.Slicing?.Rules?.ToString(),
            HasConstraints = element.Constraint is { Count: > 0 },
            HasMappings = element.Mapping is { Count: > 0 },
            ConstraintsDisplay = FormatStu3Constraints(element),
            MappingsDisplay = FormatStu3Mappings(element)
        };

    private static SnapshotElementType FromR4Type(ElementDefinition.TypeRefComponent type) =>
        new()
        {
            Code = type.Code,
            Profiles = type.Profile?.ToList() ?? [],
            TargetProfiles = type.TargetProfile?.ToList() ?? []
        };

    private static SnapshotElementType FromStu3Type(Stu3.ElementDefinition.TypeRefComponent type) =>
        new()
        {
            Code = type.Code,
            Profiles = ToProfileList(type.Profile),
            TargetProfiles = ToProfileList(type.TargetProfile)
        };

    private static IReadOnlyList<string> ToProfileList(string? profile) =>
        string.IsNullOrWhiteSpace(profile) ? [] : [profile];

    private static string FormatR4FixedPattern(ElementDefinition element)
    {
        if (element.Fixed is not null)
            return "fixed: " + Truncate(R4Serializer.SerializeToString(element.Fixed), 120);
        if (element.Pattern is not null)
            return "pattern: " + Truncate(R4Serializer.SerializeToString(element.Pattern), 120);
        return "—";
    }

    private static string FormatStu3FixedPattern(Stu3.ElementDefinition element)
    {
        if (element.Fixed is not null)
            return "fixed: " + Truncate(Stu3Serializer.SerializeToString(element.Fixed), 120);
        if (element.Pattern is not null)
            return "pattern: " + Truncate(Stu3Serializer.SerializeToString(element.Pattern), 120);
        return "—";
    }

    private static string FormatR4SlicingKey(ElementDefinition.SlicingComponent slicing)
    {
        var discriminators = slicing.Discriminator is { Count: > 0 }
            ? string.Join(",", slicing.Discriminator.Select(d => $"{d.Type}:{d.Path}"))
            : "";
        return $"{discriminators}|{slicing.Rules}|{slicing.Ordered}";
    }

    private static string FormatStu3SlicingKey(Stu3.ElementDefinition.SlicingComponent slicing)
    {
        var discriminators = slicing.Discriminator is { Count: > 0 }
            ? string.Join(",", slicing.Discriminator.Select(d => $"{d.Type}:{d.Path}"))
            : "";
        return $"{discriminators}|{slicing.Rules}|{slicing.Ordered}";
    }

    private static string? FormatR4Constraints(ElementDefinition element)
    {
        if (element.Constraint is null || element.Constraint.Count == 0)
            return null;
        return string.Join("; ", element.Constraint.Select(c =>
        {
            var key = c.Key ?? "?";
            var text = c.Human ?? c.Expression ?? "";
            return string.IsNullOrEmpty(text) ? key : $"{key}: {Truncate(text, 60)}";
        }));
    }

    private static string? FormatStu3Constraints(Stu3.ElementDefinition element)
    {
        if (element.Constraint is null || element.Constraint.Count == 0)
            return null;
        return string.Join("; ", element.Constraint.Select(c =>
        {
            var key = c.Key ?? "?";
            var text = c.Human ?? c.Expression ?? "";
            return string.IsNullOrEmpty(text) ? key : $"{key}: {Truncate(text, 60)}";
        }));
    }

    private static string? FormatR4Mappings(ElementDefinition element)
    {
        if (element.Mapping is null || element.Mapping.Count == 0)
            return null;
        return string.Join("; ", element.Mapping.Select(m => $"{m.Identity ?? "?"}→{m.Map ?? "—"}"));
    }

    private static string? FormatStu3Mappings(Stu3.ElementDefinition element)
    {
        if (element.Mapping is null || element.Mapping.Count == 0)
            return null;
        return string.Join("; ", element.Mapping.Select(m => $"{m.Identity ?? "?"}→{m.Map ?? "—"}"));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
