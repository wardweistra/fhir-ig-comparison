using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class FirelyPackageLoaderTests
{
    [Fact]
    public void Load_us_core_from_test_case_1_indexes_primary_package_resources()
    {
        var testCaseRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "test-cases", "test-case-1"));
        Assert.True(Directory.Exists(testCaseRoot), $"Missing test data at {testCaseRoot}");

        var usCoreDir = Path.Combine(testCaseRoot, "US Core");
        var files = ReadFirelyIgFolder(usCoreDir, "US Core");

        var session = new FolderPackageLoader().Load("test-case-1", files);
        var usCore = session.Packages.Single(p => p.FolderName == "US Core");

        Assert.Equal(PackageLayout.FirelyTerminal, usCore.Validation.Layout);
        Assert.True(usCore.Validation.IsValid, string.Join("; ", usCore.Validation.Errors));
        Assert.Equal("hl7.fhir.us.core", usCore.Validation.PrimaryPackageId);
        Assert.Equal("9.0.0-ballot", usCore.Validation.PrimaryPackageVersion);
        Assert.True(usCore.ResourcesByCanonical.Count > 100);
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
