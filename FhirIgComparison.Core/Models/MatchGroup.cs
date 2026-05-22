namespace FhirIgComparison.Core.Models;

public sealed class MatchGroup
{
    public required string CanonicalUrl { get; init; }
    public required string ResourceType { get; init; }
    public Dictionary<string, ResourceRef> ResourcesByIg { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int IgCount => ResourcesByIg.Count;
    public MatchCategory Category { get; set; }

    public IndexedFhirResource? GetIndexedResource(string igFolderName) =>
        ResourcesByIg.TryGetValue(igFolderName, out var r) ? r.Resource : null;
}
