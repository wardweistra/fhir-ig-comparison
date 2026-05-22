using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public sealed class ResourceCatalog
{
    private readonly IReadOnlyList<IgPackage> _packages;

    public ResourceCatalog(IReadOnlyList<IgPackage> packages)
    {
        _packages = packages.Where(p => p.Validation.IsValid).ToList();
    }

    public IReadOnlyList<string> GetValidIgFolders() =>
        _packages.Select(p => p.FolderName).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> GetResourceTypes()
    {
        return _packages
            .SelectMany(p => p.ResourcesByCanonical.Values)
            .Select(r => r.ResourceType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetStructureDefinitionBaseTypes()
    {
        return _packages
            .SelectMany(p => p.ResourcesByCanonical.Values)
            .Where(r => r.IsResourceProfile)
            .Select(r => r.StructureDefinitionType)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t!, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public IReadOnlyList<ResourceCandidate> GetCandidates(
        string igFolder,
        string resourceType,
        string? structureDefinitionBaseType = null)
    {
        var ig = _packages.FirstOrDefault(p =>
            p.FolderName.Equals(igFolder, StringComparison.OrdinalIgnoreCase));
        if (ig is null)
            return [];

        var candidates = new List<ResourceCandidate>();
        foreach (var (canonical, resource) in ig.ResourcesByCanonical)
        {
            if (!resource.ResourceType.Equals(resourceType, StringComparison.OrdinalIgnoreCase))
                continue;

            if (resource.IsStructureDefinition)
            {
                var kind = resource.StructureDefinitionKind ?? "";
                if (!string.IsNullOrEmpty(kind)
                    && !kind.Equals("resource", StringComparison.OrdinalIgnoreCase)
                    && !kind.Equals("logical", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(structureDefinitionBaseType)
                    && !string.Equals(resource.StructureDefinitionType, structureDefinitionBaseType, StringComparison.OrdinalIgnoreCase))
                    continue;

                candidates.Add(ToCandidate(ig.FolderName, canonical, resource));
            }
            else if (string.IsNullOrWhiteSpace(structureDefinitionBaseType))
            {
                candidates.Add(ToCandidate(ig.FolderName, canonical, resource));
            }
        }

        return candidates
            .OrderByDescending(c => IsLikelyIgProfile(igFolder, c) ? 1 : 0)
            .ThenBy(c => c.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string? SuggestDefault(string igFolder, string resourceType, string? structureDefinitionBaseType)
    {
        var candidates = GetCandidates(igFolder, resourceType, structureDefinitionBaseType);
        if (candidates.Count == 0)
            return null;

        var constraintProfiles = candidates
            .Where(c => IsLikelyIgProfile(igFolder, c))
            .ToList();
        if (constraintProfiles.Count > 0)
            return constraintProfiles[0].CanonicalUrl;

        return candidates[0].CanonicalUrl;
    }

    public void ApplySuggestedDefaults(ManualComparisonSelection selection)
    {
        selection.SelectedCanonicalByIg.Clear();
        foreach (var igFolder in GetValidIgFolders())
        {
            var suggested = SuggestDefault(
                igFolder,
                selection.ResourceType,
                selection.StructureDefinitionBaseType);
            if (!string.IsNullOrEmpty(suggested))
                selection.SelectedCanonicalByIg[igFolder] = suggested;
        }
    }

    private bool IsLikelyIgProfile(string igFolder, ResourceCandidate candidate)
    {
        var ig = _packages.First(p => p.FolderName.Equals(igFolder, StringComparison.OrdinalIgnoreCase));
        if (!ig.ResourcesByCanonical.TryGetValue(candidate.CanonicalUrl, out var resource))
            return false;
        return resource.IsConstraintProfile;
    }

    private static ResourceCandidate ToCandidate(string igFolder, string canonical, IndexedFhirResource resource)
    {
        var label = resource.Name ?? resource.Title ?? canonical;
        return new ResourceCandidate
        {
            IgFolderName = igFolder,
            CanonicalUrl = canonical,
            DisplayLabel = label,
            ResourceType = resource.ResourceType,
            StructureDefinitionType = resource.StructureDefinitionType,
            StructureDefinitionKind = resource.StructureDefinitionKind
        };
    }
}
