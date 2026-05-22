using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class ManualComparisonBuilderTests
{
  private static readonly string TestCaseRoot = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory, "..", "..", "..", "..", "test-cases", "test-case-1"));

  [Fact]
  public void Catalog_returns_patient_structure_definitions_per_ig()
  {
    var session = LoadTestCase1Session();
    var catalog = new ResourceCatalog(session.Packages);

    foreach (var ig in new[] { "US Core", "NL Core", "UK Core" })
    {
      var candidates = catalog.GetCandidates(ig, "StructureDefinition", "Patient");
      Assert.NotEmpty(candidates);
      Assert.Contains(candidates, c =>
          c.DisplayLabel.Contains("patient", StringComparison.OrdinalIgnoreCase)
          || c.CanonicalUrl.Contains("patient", StringComparison.OrdinalIgnoreCase));
    }
  }

  [Fact]
  public void SuggestDefault_selects_national_patient_profiles()
  {
    var session = LoadTestCase1Session();
    var catalog = new ResourceCatalog(session.Packages);

    var us = catalog.SuggestDefault("US Core", "StructureDefinition", "Patient");
    var nl = catalog.SuggestDefault("NL Core", "StructureDefinition", "Patient");
    var uk = catalog.SuggestDefault("UK Core", "StructureDefinition", "Patient");

    Assert.NotNull(us);
    Assert.NotNull(nl);
    Assert.NotNull(uk);
    Assert.Contains("us-core-patient", us, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("nl-core-Patient", nl, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("UKCore-Patient", uk, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void Build_produces_match_group_with_three_patient_profiles()
  {
    var session = LoadTestCase1Session();
    var catalog = new ResourceCatalog(session.Packages);
    var selection = new ManualComparisonSelection
    {
      ResourceType = "StructureDefinition",
      StructureDefinitionBaseType = "Patient"
    };
    catalog.ApplySuggestedDefaults(selection);

    var result = new ManualComparisonBuilder().Build(selection, session.Packages);

    Assert.True(result.Success, result.Error);
    Assert.NotNull(result.Group);
    Assert.Equal(3, result.Group.ResourcesByIg.Count);
    Assert.Equal("StructureDefinition", result.Group.ResourceType);
        foreach (var resource in result.Group.ResourcesByIg.Values)
        {
            Assert.True(resource.Resource.IsStructureDefinition);
        }
  }

  [Fact]
  public void Build_fails_when_fewer_than_two_selections()
  {
    var session = LoadTestCase1Session();
    var selection = new ManualComparisonSelection
    {
      ResourceType = "StructureDefinition",
      StructureDefinitionBaseType = "Patient",
      SelectedCanonicalByIg =
      {
        ["US Core"] = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient"
      }
    };

    var result = new ManualComparisonBuilder().Build(selection, session.Packages);

    Assert.False(result.Success);
    Assert.Contains("at least two", result.Error, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void Build_succeeds_with_partial_ig_selection()
  {
    var session = LoadTestCase1Session();
    var catalog = new ResourceCatalog(session.Packages);
    var selection = new ManualComparisonSelection
    {
      ResourceType = "StructureDefinition",
      StructureDefinitionBaseType = "Patient"
    };
    selection.SelectedCanonicalByIg["US Core"] =
        catalog.SuggestDefault("US Core", "StructureDefinition", "Patient")!;
    selection.SelectedCanonicalByIg["NL Core"] =
        catalog.SuggestDefault("NL Core", "StructureDefinition", "Patient")!;

    var result = new ManualComparisonBuilder().Build(selection, session.Packages);

    Assert.True(result.Success, result.Error);
    Assert.NotNull(result.Group);
    Assert.Equal(2, result.Group.ResourcesByIg.Count);
    Assert.False(result.Group.ResourcesByIg.ContainsKey("UK Core"));
  }

  public static ComparisonSession LoadTestCase1Session()
  {
    Assert.True(Directory.Exists(TestCaseRoot), $"Missing test data at {TestCaseRoot}");

    var files = new List<FileEntry>();
    foreach (var igName in new[] { "US Core", "NL Core", "UK Core" })
    {
      var igDir = Path.Combine(TestCaseRoot, igName);
      files.AddRange(ReadFirelyIgFolder(igDir, igName));
    }

    return new FolderPackageLoader().Load("test-case-1", files);
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
