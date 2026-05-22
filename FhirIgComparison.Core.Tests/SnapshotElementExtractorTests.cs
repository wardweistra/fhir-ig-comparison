using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class SnapshotElementExtractorTests
{
    [Fact]
    public void Extension_slices_expose_profile_urls()
    {
        var session = ManualComparisonBuilderTests.LoadTestCase1Session();
        var catalog = new ResourceCatalog(session.Packages);
        var selection = new ManualComparisonSelection
        {
            ResourceType = "StructureDefinition",
            StructureDefinitionBaseType = "Patient"
        };
        catalog.ApplySuggestedDefaults(selection);

        var usCanonical = selection.SelectedCanonicalByIg["US Core"];
        var usCore = session.Packages.Single(p => p.FolderName == "US Core");
        var patient = usCore.ResourcesByCanonical[usCanonical];

        var race = patient.Elements.First(e =>
            e.ExtensionUrls.Any(u =>
                u.Contains("us-core-race", StringComparison.OrdinalIgnoreCase)));

        Assert.Contains(
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race",
            race.ExtensionUrls);

        Assert.Equal(
            "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race (0..1)",
            DimensionCellFormatter.FormatExtension(race));
    }

    private static readonly string TestCaseRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "test-cases", "test-case-3"));

    [Fact]
    public void Stu3_binding_value_set_uses_reference_uri_not_clr_type_name()
    {
        var session = LoadTestCase3Session();
        var stu3 = session.Packages.Single(p => p.FolderName == "NL Core STU3");

        var patient = stu3.ResourcesByCanonical.Values.First(r =>
            r.CanonicalUrl.Contains("nl-core-patient", StringComparison.OrdinalIgnoreCase));

        var bound = patient.Elements
            .Where(e => !string.IsNullOrWhiteSpace(e.BindingValueSet))
            .ToList();

        Assert.NotEmpty(bound);
        Assert.All(bound, e =>
            Assert.DoesNotContain("Hl7.Fhir.Model", e.BindingValueSet, StringComparison.Ordinal));
        Assert.Contains(bound, e => e.BindingValueSet.StartsWith("http", StringComparison.OrdinalIgnoreCase));
    }

    private static Models.ComparisonSession LoadTestCase3Session()
    {
        Assert.True(Directory.Exists(TestCaseRoot), $"Missing test data at {TestCaseRoot}");

        var files = new List<Models.FileEntry>();
        foreach (var igName in new[] { "NL Core STU3" })
        {
            var igDir = Path.Combine(TestCaseRoot, igName);
            files.AddRange(ReadFirelyIgFolder(igDir, igName));
        }

        return new FolderPackageLoader().Load("test-case-3", files);
    }

    private static List<Models.FileEntry> ReadFirelyIgFolder(string igDir, string igName)
    {
        var files = new List<Models.FileEntry>();
        AddFile(files, Path.Combine(igDir, "package.json"), $"{igName}/package.json");
        AddFile(files, Path.Combine(igDir, "fhirpkg.lock.json"), $"{igName}/fhirpkg.lock.json");
        files.Add(new Models.FileEntry { Path = $"{igName}/.fhir-package-cache/.folder-marker", Bytes = [] });

        var manifest = System.Text.Json.JsonSerializer.Deserialize<Models.FhirPackageManifest>(
            File.ReadAllText(Path.Combine(igDir, "package.json")))!;
        var lockFile = System.Text.Json.JsonSerializer.Deserialize<Models.FhirPackageLock>(
            File.ReadAllText(Path.Combine(igDir, "fhirpkg.lock.json")))!;

        foreach (var primary in FirelyPackageResolver.ResolvePrimaryPackages(manifest, lockFile))
        {
            var packageDir = Path.Combine(
                igDir, ".fhir-package-cache",
                FirelyPackageResolver.GetCacheFolderName(primary.PackageId, primary.Version),
                "package");
            foreach (var jsonFile in Directory.EnumerateFiles(packageDir, "*.json", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(igDir, jsonFile).Replace('\\', '/');
                if (rel.Contains("/other/", StringComparison.OrdinalIgnoreCase)
                    || rel.Contains("/openapi/", StringComparison.OrdinalIgnoreCase)
                    || rel.EndsWith(".index.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                AddFile(files, jsonFile, $"{igName}/{rel}");
            }
        }

        return files;
    }

    private static void AddFile(List<Models.FileEntry> files, string fullPath, string entryPath)
    {
        files.Add(new Models.FileEntry { Path = entryPath, Bytes = File.ReadAllBytes(fullPath) });
    }
}
