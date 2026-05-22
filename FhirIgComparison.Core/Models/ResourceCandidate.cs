namespace FhirIgComparison.Core.Models;

public sealed class ResourceCandidate
{
    public required string IgFolderName { get; init; }
    public required string CanonicalUrl { get; init; }
    public required string DisplayLabel { get; init; }
    public required string ResourceType { get; init; }
    public string? StructureDefinitionType { get; init; }
    public string? StructureDefinitionKind { get; init; }

    public string DropdownLabel => $"{DisplayLabel} — {CanonicalUrl}";
}
