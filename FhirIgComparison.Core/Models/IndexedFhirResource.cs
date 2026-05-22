namespace FhirIgComparison.Core.Models;

public sealed class IndexedFhirResource
{
    public required string CanonicalUrl { get; init; }
    public required string ResourceType { get; init; }
    public required FhirRelease FhirRelease { get; init; }
    public string? Title { get; init; }
    public string? Name { get; init; }
    public string? BaseDefinition { get; init; }
    public string? FhirVersion { get; init; }
    public string? StructureDefinitionType { get; init; }
    public string? StructureDefinitionKind { get; init; }
    public string? Derivation { get; init; }
    public IReadOnlyList<SnapshotElement> Elements { get; init; } = [];

    public bool IsStructureDefinition =>
        ResourceType.Equals("StructureDefinition", StringComparison.OrdinalIgnoreCase);

    public bool IsResourceProfile =>
        IsStructureDefinition
        && string.Equals(StructureDefinitionKind, "resource", StringComparison.OrdinalIgnoreCase);

    public bool IsConstraintProfile =>
        IsStructureDefinition
        && string.Equals(Derivation, "constraint", StringComparison.OrdinalIgnoreCase);
}
