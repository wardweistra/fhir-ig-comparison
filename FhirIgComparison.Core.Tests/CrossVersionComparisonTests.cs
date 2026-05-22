using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class CrossVersionComparisonTests
{
    private static readonly string TestCaseRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "test-cases", "test-case-3"));

    [Fact]
    public void Load_test_case_3_indexes_stu3_patient_profile()
    {
        var session = LoadTestCase3Session();
        var stu3 = session.Packages.Single(p => p.FolderName == "NL Core STU3");

        Assert.True(stu3.Validation.IsValid, string.Join("; ", stu3.Validation.Errors));
        Assert.Equal(FhirRelease.STU3, stu3.FhirRelease);
        Assert.True(stu3.ResourcesByCanonical.Count > 0);

        var patientProfiles = stu3.ResourcesByCanonical.Values
            .Where(r => r.IsResourceProfile
                && string.Equals(r.StructureDefinitionType, "Patient", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(patientProfiles);
        Assert.Contains(patientProfiles, p =>
            p.CanonicalUrl.Contains("nl-core-patient", StringComparison.OrdinalIgnoreCase)
            || (p.Name?.Contains("nl-core", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public void Manual_compare_r4_and_stu3_patient_profiles_produces_rows()
    {
        var session = LoadTestCase3Session();
        var catalog = new ResourceCatalog(session.Packages);

        var r4Canonical = catalog.GetCandidates("NL Core R4", "StructureDefinition", "Patient")
            .First(c => c.CanonicalUrl.Contains("nl-core-Patient", StringComparison.OrdinalIgnoreCase)
                || c.DisplayLabel.Contains("nl-core-Patient", StringComparison.OrdinalIgnoreCase))
            .CanonicalUrl;

        var stu3Canonical = catalog.GetCandidates("NL Core STU3", "StructureDefinition", "Patient")
            .First(c => c.CanonicalUrl.Contains("nl-core-patient", StringComparison.OrdinalIgnoreCase)
                || c.DisplayLabel.Contains("nl-core-patient", StringComparison.OrdinalIgnoreCase))
            .CanonicalUrl;

        var selection = new ManualComparisonSelection
        {
            ResourceType = "StructureDefinition",
            StructureDefinitionBaseType = "Patient",
            SelectedCanonicalByIg =
            {
                ["NL Core R4"] = r4Canonical,
                ["NL Core STU3"] = stu3Canonical
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

    private static ComparisonSession LoadTestCase3Session()
    {
        Assert.True(Directory.Exists(TestCaseRoot), $"Missing test data at {TestCaseRoot}");

        var files = new List<FileEntry>();
        foreach (var igName in new[] { "NL Core R4", "NL Core STU3" })
        {
            var igDir = Path.Combine(TestCaseRoot, igName);
            files.AddRange(ReadFirelyIgFolder(igDir, igName));
        }

        return new FolderPackageLoader().Load("test-case-3", files);
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
