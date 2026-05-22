using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public sealed class ManualComparisonBuilder
{
    public ManualComparisonResult Build(
        ManualComparisonSelection selection,
        IReadOnlyList<IgPackage> packages)
    {
        var validPackages = packages.Where(p => p.Validation.IsValid).ToList();
        if (validPackages.Count == 0)
            return ManualComparisonResult.Fail("No valid IG folders loaded.");

        if (string.IsNullOrWhiteSpace(selection.ResourceType))
            return ManualComparisonResult.Fail("Select a resource type.");

        var selectedPackages = validPackages
            .Where(ig => selection.SelectedCanonicalByIg.TryGetValue(ig.FolderName, out var canonical)
                && !string.IsNullOrWhiteSpace(canonical))
            .ToList();

        if (selectedPackages.Count < 2)
            return ManualComparisonResult.Fail("Select at least two resources to compare.");

        foreach (var ig in selectedPackages)
        {
            var canonical = selection.SelectedCanonicalByIg[ig.FolderName];

            if (!ig.ResourcesByCanonical.TryGetValue(canonical, out var resource))
                return ManualComparisonResult.Fail($"Selected resource not found in \"{ig.FolderName}\".");

            if (!resource.ResourceType.Equals(selection.ResourceType, StringComparison.OrdinalIgnoreCase))
                return ManualComparisonResult.Fail("All selected resources must be the same type.");
        }

        var firstCanonical = selection.SelectedCanonicalByIg[selectedPackages[0].FolderName];
        var firstResource = selectedPackages[0].ResourcesByCanonical[firstCanonical];

        var group = new MatchGroup
        {
            CanonicalUrl = $"manual:{selection.ResourceType}:{selection.StructureDefinitionBaseType ?? "any"}",
            ResourceType = firstResource.ResourceType
        };

        foreach (var ig in selectedPackages)
        {
            var canonical = selection.SelectedCanonicalByIg[ig.FolderName];
            var resource = ig.ResourcesByCanonical[canonical];
            group.ResourcesByIg[ig.FolderName] = new ResourceRef
            {
                IgFolderName = ig.FolderName,
                Resource = resource,
                CanonicalUrl = canonical,
                ResourceType = group.ResourceType
            };
        }

        group.Category = MatchCategory.Partial;
        return ManualComparisonResult.Ok(group);
    }
}

public sealed class ManualComparisonResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public MatchGroup? Group { get; init; }

    public static ManualComparisonResult Ok(MatchGroup group) =>
        new() { Success = true, Group = group };

    public static ManualComparisonResult Fail(string error) =>
        new() { Success = false, Error = error };
}
