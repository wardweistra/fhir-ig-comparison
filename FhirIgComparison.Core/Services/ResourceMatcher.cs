using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public sealed class ResourceMatcher
{
    public List<MatchGroup> Match(IReadOnlyList<IgPackage> packages)
    {
        var validPackages = packages.Where(p => p.Validation.IsValid).ToList();
        if (validPackages.Count == 0)
            return [];

        var canonicalIndex = new Dictionary<string, MatchGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var ig in validPackages)
        {
            foreach (var (canonical, resource) in ig.ResourcesByCanonical)
            {
                if (!canonicalIndex.TryGetValue(canonical, out var group))
                {
                    group = new MatchGroup
                    {
                        CanonicalUrl = canonical,
                        ResourceType = resource.ResourceType
                    };
                    canonicalIndex[canonical] = group;
                }

                if (group.ResourcesByIg.ContainsKey(ig.FolderName))
                    continue;

                group.ResourcesByIg[ig.FolderName] = new ResourceRef
                {
                    IgFolderName = ig.FolderName,
                    Resource = resource,
                    CanonicalUrl = canonical,
                    ResourceType = group.ResourceType
                };
            }
        }

        var totalIgs = validPackages.Count;
        foreach (var group in canonicalIndex.Values)
        {
            group.Category = group.IgCount switch
            {
                1 => MatchCategory.Unique,
                var n when n == totalIgs => MatchCategory.Full,
                _ => MatchCategory.Partial
            };
        }

        return canonicalIndex.Values
            .OrderBy(g => g.ResourceType)
            .ThenBy(g => g.CanonicalUrl)
            .ToList();
    }
}
