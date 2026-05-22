namespace FhirIgComparison.Core.Models;

public sealed class ResourceRef
{
    public required string IgFolderName { get; init; }
    public required IndexedFhirResource Resource { get; init; }
    public required string CanonicalUrl { get; init; }
    public required string ResourceType { get; init; }
}
