using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class StructureDefinitionComparerTests
{
    private static readonly string TestCaseRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "test-cases", "test-case-1"));

    [Fact]
    public void Compare_patient_profiles_produces_summary_and_differing_rows()
    {
        var session = ManualComparisonBuilderTests.LoadTestCase1Session();
        var catalog = new ResourceCatalog(session.Packages);
        var selection = new ManualComparisonSelection
        {
            ResourceType = "StructureDefinition",
            StructureDefinitionBaseType = "Patient"
        };
        catalog.ApplySuggestedDefaults(selection);

        var buildResult = new ManualComparisonBuilder().Build(selection, session.Packages);
        Assert.True(buildResult.Success, buildResult.Error);

        var igOrder = session.Packages
            .Where(p => p.Validation.IsValid)
            .Select(p => p.FolderName)
            .ToList();

        var comparison = new StructureDefinitionComparer().Compare(
            buildResult.Group!, igOrder, session.Packages);

        Assert.Equal(3, comparison.Summary.Columns.Count);
        Assert.True(comparison.Summary.Columns.All(c => c.ConstrainedElementCount > 0));
        Assert.Contains(comparison.Summary.Columns, c => c.MustSupportCount > 0);
        Assert.NotEmpty(comparison.Rows);

        var cardinalityDiffs = comparison.Rows.Count(r =>
            r.HasDifference(ComparisonDimension.Cardinality));
        Assert.True(cardinalityDiffs > 0);
    }

    [Fact]
    public void Compare_rows_follow_snapshot_order_not_alphabetical()
    {
        var session = ManualComparisonBuilderTests.LoadTestCase1Session();
        var catalog = new ResourceCatalog(session.Packages);
        var selection = new ManualComparisonSelection
        {
            ResourceType = "StructureDefinition",
            StructureDefinitionBaseType = "Patient"
        };
        catalog.ApplySuggestedDefaults(selection);

        var buildResult = new ManualComparisonBuilder().Build(selection, session.Packages);
        Assert.True(buildResult.Success, buildResult.Error);

        var igOrder = session.Packages
            .Where(p => p.Validation.IsValid)
            .Select(p => p.FolderName)
            .ToList();

        var comparison = new StructureDefinitionComparer().Compare(
            buildResult.Group!, igOrder, session.Packages);

        Assert.Equal("Patient", comparison.Rows[0].Path);
        Assert.Null(comparison.Rows[0].SliceName);

        var rows = comparison.Rows.ToList();
        var raceIndex = rows.FindIndex(r =>
            r.Path == "Patient.extension"
            && string.Equals(r.SliceName, "race", StringComparison.OrdinalIgnoreCase));
        var ethnicityIndex = rows.FindIndex(r =>
            r.Path == "Patient.extension"
            && string.Equals(r.SliceName, "ethnicity", StringComparison.OrdinalIgnoreCase));

        Assert.True(raceIndex >= 0);
        Assert.True(ethnicityIndex >= 0);
        Assert.True(raceIndex < ethnicityIndex);
    }

    [Fact]
    public void Compare_keeps_extension_rows_separate_when_element_ids_differ()
    {
        const string raceUrl = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race";
        var group = BuildSyntheticGroup(
            ("IG-A", [ExtensionSlice("Patient.extension", "race", raceUrl)]),
            ("IG-B", [ExtensionSlice("Patient.extension", "usCoreRace", raceUrl)]));

        var comparison = CompareSynthetic(group, ["IG-A", "IG-B"]);

        var rows = comparison.Rows
            .Where(r => r.Path == "Patient.extension")
            .ToList();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.ElementId == "Patient.extension:race");
        Assert.Contains(rows, r => r.ElementId == "Patient.extension:usCoreRace");
    }

    [Fact]
    public void Compare_does_not_merge_extension_url_rows_for_different_extension_slices()
    {
        var session = ManualComparisonBuilderTests.LoadTestCase1Session();
        var catalog = new ResourceCatalog(session.Packages);
        var selection = new ManualComparisonSelection
        {
            ResourceType = "StructureDefinition",
            StructureDefinitionBaseType = "Patient"
        };
        catalog.ApplySuggestedDefaults(selection);

        var buildResult = new ManualComparisonBuilder().Build(selection, session.Packages);
        Assert.True(buildResult.Success, buildResult.Error);

        var igOrder = session.Packages
            .Where(p => p.Validation.IsValid)
            .Select(p => p.FolderName)
            .ToList();

        var comparison = new StructureDefinitionComparer().Compare(
            buildResult.Group!, igOrder, session.Packages);

        var nationalityUrl = comparison.Rows.Single(r =>
            r.ElementId == "Patient.extension:nationality.url");
        var birthPlaceUrl = comparison.Rows.Single(r =>
            r.ElementId == "Patient.extension:birthPlace.url");

        Assert.NotNull(nationalityUrl.ElementsByIg[igOrder.IndexOf("NL Core")]);
        Assert.Null(nationalityUrl.ElementsByIg[igOrder.IndexOf("UK Core")]);
        Assert.Null(birthPlaceUrl.ElementsByIg[igOrder.IndexOf("NL Core")]);
        Assert.NotNull(birthPlaceUrl.ElementsByIg[igOrder.IndexOf("UK Core")]);
    }

    [Fact]
    public void Compare_keeps_extension_rows_separate_when_urls_differ()
    {
        var group = BuildSyntheticGroup(
            ("IG-A", [ExtensionSlice("Patient.extension", "race",
                "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race")]),
            ("IG-B", [ExtensionSlice("Patient.extension", "ethnicity",
                "http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity")]));

        var comparison = CompareSynthetic(group, ["IG-A", "IG-B"]);

        var extensionRows = comparison.Rows
            .Where(r => r.Path == "Patient.extension" && r.ExtensionUrl is not null)
            .ToList();

        Assert.Equal(2, extensionRows.Count);
        Assert.Equal(2, extensionRows.Count(r => r.ElementsByIg.Count(e => e is not null) == 1));
    }

    [Fact]
    public void Compare_keeps_same_extension_url_on_different_paths_separate()
    {
        const string url = "https://fhir.example/base/StructureDefinition/date-accuracy-indicator";
        var group = BuildSyntheticGroup(
            ("IG-A",
            [
                ExtensionSlice("Patient.birthDate.extension", "accuracyIndicator", url),
                ExtensionSlice("Patient.deceased[x].extension", "accuracyIndicator", url)
            ]),
            ("IG-B",
            [
                ExtensionSlice("Patient.birthDate.extension", "accuracyIndicator", url),
                ExtensionSlice("Patient.deceased[x].extension", "accuracyIndicator", url)
            ]));

        var comparison = CompareSynthetic(group, ["IG-A", "IG-B"]);

        var rows = comparison.Rows
            .Where(r => r.ExtensionUrl == url)
            .ToList();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Path == "Patient.birthDate.extension");
        Assert.Contains(rows, r => r.Path == "Patient.deceased[x].extension");
    }

    [Fact]
    public void Compare_keeps_unsliced_extension_rows_separate_by_element_id()
    {
        var group = BuildSyntheticGroup(
            ("IG-A", [new SnapshotElement { Path = "Patient.extension", SliceName = "customA", ElementId = "Patient.extension:customA" }]),
            ("IG-B", [new SnapshotElement { Path = "Patient.extension", SliceName = "customB", ElementId = "Patient.extension:customB" }]));

        var comparison = CompareSynthetic(group, ["IG-A", "IG-B"]);

        var rows = comparison.Rows
            .Where(r => r.Path == "Patient.extension" && r.ExtensionUrl is null)
            .ToList();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.SliceName == "customA");
        Assert.Contains(rows, r => r.SliceName == "customB");
    }

    private static ProfileComparisonResult CompareSynthetic(MatchGroup group, IReadOnlyList<string> igOrder) =>
        new StructureDefinitionComparer().Compare(group, igOrder, []);

    private static MatchGroup BuildSyntheticGroup(
        params (string Ig, SnapshotElement[] Elements)[] profiles)
    {
        var group = new MatchGroup
        {
            CanonicalUrl = "http://example.org/StructureDefinition/synthetic-patient",
            ResourceType = "StructureDefinition"
        };

        foreach (var (ig, elements) in profiles)
        {
            group.ResourcesByIg[ig] = new ResourceRef
            {
                IgFolderName = ig,
                CanonicalUrl = group.CanonicalUrl,
                ResourceType = "StructureDefinition",
                Resource = new IndexedFhirResource
                {
                    CanonicalUrl = group.CanonicalUrl,
                    ResourceType = "StructureDefinition",
                    FhirRelease = FhirRelease.R4,
                    Elements = elements
                }
            };
        }

        return group;
    }

    private static SnapshotElement ExtensionSlice(string path, string sliceName, string url) =>
        new()
        {
            Path = path,
            SliceName = sliceName,
            ElementId = $"{path}:{sliceName}",
            ExtensionUrls = [url],
            Types =
            [
                new SnapshotElementType
                {
                    Code = "Extension",
                    Profiles = [url]
                }
            ]
        };
}
