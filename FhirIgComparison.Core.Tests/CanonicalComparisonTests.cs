using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class CanonicalComparisonTests
{
    private static readonly string TestCaseRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "test-cases", "test-case-2"));

    [Fact]
    public void Compare_partial_canonical_match_uses_only_igs_in_group()
    {
        Assert.True(Directory.Exists(TestCaseRoot), $"Missing test data at {TestCaseRoot}");

        var session = LoadTestCase2Session();
        var group = session.MatchGroups.First(g =>
            g.CanonicalUrl.Contains("ext-AdditionalCategory", StringComparison.OrdinalIgnoreCase)
            && g.IgCount >= 2);

        var allIgOrder = session.Packages
            .Where(p => p.Validation.IsValid)
            .Select(p => p.FolderName)
            .ToList();

        var groupIgOrder = session.Packages
            .Where(p => p.Validation.IsValid && group.ResourcesByIg.ContainsKey(p.FolderName))
            .Select(p => p.FolderName)
            .ToList();

        Assert.True(groupIgOrder.Count < allIgOrder.Count,
            "Fixture should be a partial match across NL Core versions.");

        var wrongResult = new StructureDefinitionComparer().Compare(group, allIgOrder, session.Packages);
        Assert.Empty(wrongResult.Rows);
        Assert.Empty(wrongResult.Summary.Columns);

        var result = new StructureDefinitionComparer().Compare(group, groupIgOrder, session.Packages);
        Assert.Equal(groupIgOrder.Count, result.Summary.Columns.Count);
        Assert.NotEmpty(result.Rows);
    }

    private static ComparisonSession LoadTestCase2Session()
    {
        var files = new List<FileEntry>();
        foreach (var igName in Directory.EnumerateDirectories(TestCaseRoot)
                     .Select(Path.GetFileName)
                     .Where(n => n is not null)!)
        {
            files.AddRange(ReadFirelyIgFolder(Path.Combine(TestCaseRoot, igName), igName));
        }

        var session = new FolderPackageLoader().Load("test-case-2", files);
        session.MatchGroups = new ResourceMatcher().Match(session.Packages);
        return session;
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
