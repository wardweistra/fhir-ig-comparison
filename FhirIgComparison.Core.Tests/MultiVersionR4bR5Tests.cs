using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class MultiVersionR4bR5Tests
{
    private static readonly string TestCaseRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "test-cases", "test-case-4"));

    [Fact]
    public void Load_test_case_4_indexes_ee_base_r5_patient_profile()
    {
        var session = LoadTestCase4Session(["EE Base (R5)", "UK Core (R4)"]);
        var eeBase = session.Packages.Single(p => p.FolderName == "EE Base (R5)");

        Assert.True(eeBase.Validation.IsValid, string.Join("; ", eeBase.Validation.Errors));
        Assert.Equal(FhirRelease.R5, eeBase.FhirRelease);

        var patientProfiles = eeBase.ResourcesByCanonical.Values
            .Where(r => r.IsResourceProfile
                && string.Equals(r.StructureDefinitionType, "Patient", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(patientProfiles);
        Assert.Contains(patientProfiles, p =>
            p.CanonicalUrl.Contains("ee-patient", StringComparison.OrdinalIgnoreCase)
            || (p.Name?.Contains("ee-patient", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public void R4b_parser_indexes_patient_structure_definition()
    {
        var patientSdPath = Path.Combine(
            TestCaseRoot,
            "PT Core (R4B)",
            ".fhir-package-cache",
            "hl7.fhir.r4b.core#4.3.0",
            "package",
            "StructureDefinition-Patient.json");
        Assert.True(File.Exists(patientSdPath), $"Missing test data at {patientSdPath}");

        var json = File.ReadAllText(patientSdPath);
        var indexer = new FhirResourceIndexer();

        Assert.True(indexer.TryIndex(json, FhirRelease.R4B, out var resource));
        Assert.NotNull(resource);
        Assert.Equal(FhirRelease.R4B, resource.FhirRelease);
        Assert.True(resource.Elements.Count > 0);
    }

    [Fact]
    public void Manual_compare_r5_and_r4_patient_profiles_produces_rows()
    {
        var session = LoadTestCase4Session(["EE Base (R5)", "UK Core (R4)"]);
        var catalog = new ResourceCatalog(session.Packages);

        var r5Canonical = catalog.GetCandidates("EE Base (R5)", "StructureDefinition", "Patient")
            .First(c => c.CanonicalUrl.Contains("ee-patient", StringComparison.OrdinalIgnoreCase)
                || c.DisplayLabel.Contains("ee-patient", StringComparison.OrdinalIgnoreCase))
            .CanonicalUrl;

        var r4Canonical = catalog.GetCandidates("UK Core (R4)", "StructureDefinition", "Patient")
            .First(c => c.CanonicalUrl.Contains("UKCore-Patient", StringComparison.OrdinalIgnoreCase)
                || c.DisplayLabel.Contains("UKCore", StringComparison.OrdinalIgnoreCase))
            .CanonicalUrl;

        var selection = new ManualComparisonSelection
        {
            ResourceType = "StructureDefinition",
            StructureDefinitionBaseType = "Patient",
            SelectedCanonicalByIg =
            {
                ["EE Base (R5)"] = r5Canonical,
                ["UK Core (R4)"] = r4Canonical
            }
        };

        var buildResult = new ManualComparisonBuilder().Build(selection, session.Packages);
        Assert.True(buildResult.Success, buildResult.Error);

        var igOrder = session.Packages
            .Where(p => p.Validation.IsValid)
            .Select(p => p.FolderName)
            .ToList();

        var comparison = new StructureDefinitionComparer().Compare(
            buildResult.Group!, igOrder, session.Packages);

        Assert.Equal(2, comparison.Summary.Columns.Count);
        Assert.True(comparison.Summary.HasFhirVersionMismatch);
        Assert.NotEmpty(comparison.Rows);
    }

    private static ComparisonSession LoadTestCase4Session(IReadOnlyList<string> igNames)
    {
        Assert.True(Directory.Exists(TestCaseRoot), $"Missing test data at {TestCaseRoot}");

        var files = new List<FileEntry>();
        foreach (var igName in igNames)
        {
            var igDir = Path.Combine(TestCaseRoot, igName);
            files.AddRange(ReadFirelyIgFolder(igDir, igName));
        }

        return new FolderPackageLoader().Load("test-case-4", files);
    }

    private static List<FileEntry> ReadFirelyIgFolder(string igDir, string igName)
    {
        var files = new List<FileEntry>();
        AddFile(files, Path.Combine(igDir, "package.json"), $"{igName}/package.json");
        AddFile(files, Path.Combine(igDir, "fhirpkg.lock.json"), $"{igName}/fhirpkg.lock.json");
        files.Add(new FileEntry { Path = $"{igName}/.fhir-package-cache/.folder-marker", Bytes = [] });

        var manifest = System.Text.Json.JsonSerializer.Deserialize<FhirPackageManifest>(
            File.ReadAllText(Path.Combine(igDir, "package.json")))!;
        var lockFile = System.Text.Json.JsonSerializer.Deserialize<FhirPackageLock>(
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

    private static void AddFile(List<FileEntry> files, string fullPath, string entryPath)
    {
        files.Add(new FileEntry { Path = entryPath, Bytes = File.ReadAllBytes(fullPath) });
    }
}
