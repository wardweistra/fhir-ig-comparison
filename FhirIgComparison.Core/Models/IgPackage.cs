namespace FhirIgComparison.Core.Models;

public sealed class IgPackage
{
    public required string FolderName { get; init; }
    public required FhirPackageManifest Manifest { get; init; }
    public required IgValidation Validation { get; init; }
    public FhirRelease FhirRelease { get; init; } = FhirRelease.R4;
    public Dictionary<string, IndexedFhirResource> ResourcesByCanonical { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
