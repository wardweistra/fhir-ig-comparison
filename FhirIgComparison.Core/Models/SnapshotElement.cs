namespace FhirIgComparison.Core.Models;

public sealed class SnapshotElement
{
    public required string Path { get; init; }
    public string? SliceName { get; init; }
    public string? ElementId { get; init; }
    public int? Min { get; init; }
    public string? Max { get; init; }
    public bool? MustSupport { get; init; }
    public string? BindingValueSet { get; init; }
    public string? BindingStrength { get; init; }
    public IReadOnlyList<SnapshotElementType> Types { get; init; } = [];
    public string? FixedPatternDisplay { get; init; }
    public IReadOnlyList<string> ExtensionUrls { get; init; } = [];
    public string? SlicingKey { get; init; }
    public string? SlicingRules { get; init; }
    public bool HasConstraints { get; init; }
    public bool HasMappings { get; init; }
    public string? ConstraintsDisplay { get; init; }
    public string? MappingsDisplay { get; init; }
}

public sealed class SnapshotElementType
{
    public string? Code { get; init; }
    public IReadOnlyList<string> Profiles { get; init; } = [];
    public IReadOnlyList<string> TargetProfiles { get; init; } = [];
}
