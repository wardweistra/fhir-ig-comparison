using FhirIgComparison.Core;
using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public sealed class ProfileSummaryBuilder
{
    public ProfileComparisonSummary Build(
        IReadOnlyList<IndexedFhirResource> profiles,
        IReadOnlyList<string> igOrder,
        IReadOnlyList<IgPackage> packages)
    {
        var packageByFolder = packages.ToDictionary(p => p.FolderName, StringComparer.OrdinalIgnoreCase);
        var columns = new List<ProfileColumnSummary>();

        for (var i = 0; i < igOrder.Count; i++)
        {
            var ig = igOrder[i];
            var profile = profiles[i];
            if (profile is null)
                continue;

            packageByFolder.TryGetValue(ig, out var package);
            var fhirVersion = profile.FhirVersion
                ?? package?.Manifest.FhirVersions?.FirstOrDefault()
                ?? package?.FhirRelease.ToString()
                ?? "—";

            var elements = profile.Elements;
            columns.Add(new ProfileColumnSummary
            {
                IgFolderName = ig,
                ProfileName = profile.Title ?? profile.Name ?? ig,
                CanonicalUrl = profile.CanonicalUrl,
                PackageScope = SimplifierUrls.GetPackageScope(package),
                BaseDefinition = profile.BaseDefinition ?? "—",
                FhirVersion = fhirVersion,
                ConstrainedElementCount = elements.Count(IsConstrainedElement),
                MustSupportCount = elements.Count(e => e.MustSupport == true),
                ExtensionCount = elements.Count(IsExtensionElement)
            });
        }

        var versions = columns
            .Select(c => c.FhirVersion)
            .Where(v => !string.IsNullOrEmpty(v) && v != "—")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProfileComparisonSummary
        {
            Columns = columns,
            HasFhirVersionMismatch = versions.Count > 1
        };
    }

    private static bool IsConstrainedElement(SnapshotElement element)
    {
        if ((element.Min ?? 0) > 0)
            return true;
        if (!string.IsNullOrEmpty(element.Max) && element.Max != "*")
            return true;
        if (element.MustSupport == true)
            return true;
        if (!string.IsNullOrWhiteSpace(element.BindingValueSet))
            return true;
        if (element.Types.Any(t => t.Profiles.Count > 0 || t.TargetProfiles.Count > 0))
            return true;
        if (!string.IsNullOrWhiteSpace(element.SlicingKey))
            return true;
        if (element.FixedPatternDisplay is not null && element.FixedPatternDisplay != "—")
            return true;
        if (element.HasConstraints)
            return true;
        return false;
    }

    private static bool IsExtensionElement(SnapshotElement element) =>
        element.Path.Contains("extension", StringComparison.OrdinalIgnoreCase)
        || element.Types.Any(t =>
            t.Code?.Equals("Extension", StringComparison.OrdinalIgnoreCase) == true);
}
