using FhirIgComparison.Core;
using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public sealed class StructureDefinitionComparer
{
    private readonly DimensionCellFormatter _formatter = new();
    private readonly CompatibilityAnalyzer _analyzer = new();
    private readonly ProfileSummaryBuilder _summaryBuilder = new();

    public ProfileComparisonResult Compare(
        MatchGroup group,
        IReadOnlyList<string> igOrder,
        IReadOnlyList<IgPackage> packages)
    {
        var profiles = igOrder.Select(group.GetIndexedResource).ToList();
        if (profiles.Count == 0 || profiles.Any(p => p is null))
        {
            return new ProfileComparisonResult
            {
                Summary = new ProfileComparisonSummary(),
                IgOrder = igOrder,
                PackageScopeByIg = BuildPackageScopeByIg(igOrder, packages),
                Rows = []
            };
        }

        var mergeKeys = BuildMergeKeys(profiles!);
        var rows = new List<ElementComparisonRow>();

        foreach (var key in mergeKeys)
        {
            var elements = profiles!
                .Select(p => FindElement(p!, key.ElementId))
                .ToList();

            var displayByDimension = new Dictionary<ComparisonDimension, IReadOnlyList<string>>();
            foreach (var dimension in Enum.GetValues<ComparisonDimension>())
            {
                displayByDimension[dimension] = _formatter.FormatAll(dimension, elements);
            }

            var first = elements.FirstOrDefault(e => e is not null);
            var sliceName = elements.FirstOrDefault(e => !string.IsNullOrEmpty(e?.SliceName))?.SliceName;

            rows.Add(new ElementComparisonRow
            {
                Path = first?.Path ?? key.ElementId,
                SliceName = sliceName,
                ExtensionUrl = first is not null
                    ? ExtensionElementIdentity.GetPrimaryExtensionUrl(first)
                    : null,
                ElementId = key.ElementId,
                ElementsByIg = elements,
                DisplayValuesByDimension = displayByDimension,
                CompatibilityByDimension = _analyzer.Analyze(elements)
            });
        }

        return new ProfileComparisonResult
        {
            Summary = _summaryBuilder.Build(profiles!, igOrder, packages),
            IgOrder = igOrder,
            PackageScopeByIg = BuildPackageScopeByIg(igOrder, packages),
            Rows = rows
        };
    }

    private static IReadOnlyDictionary<string, string?> BuildPackageScopeByIg(
        IReadOnlyList<string> igOrder,
        IReadOnlyList<IgPackage> packages)
    {
        var packageByFolder = packages.ToDictionary(p => p.FolderName, StringComparer.OrdinalIgnoreCase);
        return igOrder.ToDictionary(
            ig => ig,
            ig => packageByFolder.TryGetValue(ig, out var package)
                ? SimplifierUrls.GetPackageScope(package)
                : null,
            StringComparer.OrdinalIgnoreCase);
    }

    private static List<ElementMergeKey> BuildMergeKeys(IReadOnlyList<IndexedFhirResource> profiles)
    {
        var keys = new Dictionary<string, ElementMergeKey>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var profile in profiles)
        {
            if (profile is null)
                continue;

            foreach (var element in profile.Elements)
            {
                if (string.IsNullOrEmpty(element.ElementId))
                    continue;

                var key = ElementMergeKey.From(element);
                if (keys.TryAdd(key.Key, key))
                    order.Add(key.Key);
            }
        }

        return order.Select(k => keys[k]).ToList();
    }

    private static SnapshotElement? FindElement(IndexedFhirResource profile, string elementId) =>
        profile.Elements.FirstOrDefault(e =>
            string.Equals(e.ElementId, elementId, StringComparison.Ordinal));

    private sealed record ElementMergeKey(string ElementId)
    {
        public string Key => ElementId;

        public static ElementMergeKey From(SnapshotElement element) =>
            new(element.ElementId ?? element.Path);
    }
}
