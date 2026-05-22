using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class CompatibilityAnalyzerTests
{
    [Fact]
    public void Cardinality_conflict_when_ranges_do_not_overlap()
    {
        var elements = new SnapshotElement?[]
        {
            ElementWithCardinality(1, "1"),
            ElementWithCardinality(0, "0")
        };

        var result = CompatibilityAnalyzer.AnalyzeCardinality(elements);

        Assert.Equal(CompatibilityStatus.Conflict, result.Status);
        Assert.Equal("1..0", result.CombinedValue);
    }

    [Fact]
    public void Cardinality_compatible_intersection_for_overlapping_ranges()
    {
        var elements = new SnapshotElement?[]
        {
            ElementWithCardinality(1, "*"),
            ElementWithCardinality(0, "*")
        };

        var result = CompatibilityAnalyzer.AnalyzeCardinality(elements);

        Assert.Equal(CompatibilityStatus.Warning, result.Status);
        Assert.Equal("1..*", result.CombinedValue);
    }

    [Fact]
    public void MustSupport_union_is_always_compatible()
    {
        var elements = new SnapshotElement?[]
        {
            new SnapshotElement { Path = "Test", MustSupport = true },
            new SnapshotElement { Path = "Test", MustSupport = false }
        };

        var result = CompatibilityAnalyzer.AnalyzeMustSupport(elements);

        Assert.Equal(CompatibilityStatus.Compatible, result.Status);
        Assert.Contains("1 profile", result.CombinedValue);
    }

    [Fact]
    public void Binding_conflict_when_required_value_sets_differ()
    {
        var elements = new SnapshotElement?[]
        {
            ElementWithBinding("http://example.org/vs-a", "Required"),
            ElementWithBinding("http://example.org/vs-b", "Required")
        };

        var result = CompatibilityAnalyzer.AnalyzeBinding(elements);

        Assert.Equal(CompatibilityStatus.Conflict, result.Status);
    }

    [Fact]
    public void Binding_compatible_unversioned_and_versioned()
    {
        const string baseUrl = "http://hl7.org/fhir/ValueSet/languages";
        const string versionedUrl = "http://hl7.org/fhir/ValueSet/languages|4.0.1";

        var elements = new SnapshotElement?[]
        {
            ElementWithBinding(baseUrl, "Required"),
            ElementWithBinding(versionedUrl, "Required")
        };

        var result = CompatibilityAnalyzer.AnalyzeBinding(elements);

        Assert.Equal(CompatibilityStatus.Compatible, result.Status);
        Assert.Equal(versionedUrl, result.CombinedValue);
    }

    [Fact]
    public void Binding_compatible_both_versioned_same_version()
    {
        const string versionedUrl = "http://hl7.org/fhir/ValueSet/languages|4.0.1";

        var elements = new SnapshotElement?[]
        {
            ElementWithBinding(versionedUrl, "Required"),
            ElementWithBinding(versionedUrl, "Required")
        };

        var result = CompatibilityAnalyzer.AnalyzeBinding(elements);

        Assert.Equal(CompatibilityStatus.Compatible, result.Status);
        Assert.Equal(versionedUrl, result.CombinedValue);
    }

    [Fact]
    public void Binding_conflict_different_versions()
    {
        var elements = new SnapshotElement?[]
        {
            ElementWithBinding("http://hl7.org/fhir/ValueSet/languages|4.0.1", "Required"),
            ElementWithBinding("http://hl7.org/fhir/ValueSet/languages|5.0.0", "Required")
        };

        var result = CompatibilityAnalyzer.AnalyzeBinding(elements);

        Assert.Equal(CompatibilityStatus.Conflict, result.Status);
        Assert.Equal("incompatible", result.CombinedValue);
    }

    [Fact]
    public void Binding_compatible_both_unversioned()
    {
        const string baseUrl = "http://hl7.org/fhir/ValueSet/languages";

        var elements = new SnapshotElement?[]
        {
            ElementWithBinding(baseUrl, "Required"),
            ElementWithBinding(baseUrl, "Required")
        };

        var result = CompatibilityAnalyzer.AnalyzeBinding(elements);

        Assert.Equal(CompatibilityStatus.Compatible, result.Status);
        Assert.Equal(baseUrl, result.CombinedValue);
    }

    [Fact]
    public void Extensions_conflict_when_profiles_constrain_different_urls_on_same_row()
    {
        var elements = new SnapshotElement?[]
        {
            ExtensionElement("http://example.org/ext-a"),
            ExtensionElement("http://example.org/ext-b")
        };

        var result = CompatibilityAnalyzer.AnalyzeExtensions(elements);

        Assert.Equal(CompatibilityStatus.Conflict, result.Status);
        Assert.Equal("conflict", result.CombinedValue);
    }

    [Fact]
    public void Extensions_gap_when_required_in_some_profiles_but_absent_in_others()
    {
        var elements = new SnapshotElement?[]
        {
            ExtensionElement("http://example.org/ext-a", min: 1),
            null
        };

        var result = CompatibilityAnalyzer.AnalyzeExtensions(elements);

        Assert.Equal(CompatibilityStatus.Warning, result.Status);
        Assert.Equal("gap", result.CombinedValue);
    }

    [Fact]
    public void Extensions_compatible_when_same_url_or_absent_profiles()
    {
        var elements = new SnapshotElement?[]
        {
            ExtensionElement("http://example.org/ext-a"),
            ExtensionElement("http://example.org/ext-a")
        };

        var result = CompatibilityAnalyzer.AnalyzeExtensions(elements);

        Assert.Equal(CompatibilityStatus.Compatible, result.Status);
        Assert.Equal("additive", result.CombinedValue);
    }

    private static SnapshotElement ElementWithCardinality(int min, string max) =>
        new() { Path = "Test", Min = min, Max = max };

    private static SnapshotElement ElementWithBinding(string valueSet, string strength) =>
        new() { Path = "Test", BindingValueSet = valueSet, BindingStrength = strength };

    private static SnapshotElement ExtensionElement(string url, int min = 0) =>
        new()
        {
            Path = "Patient.extension",
            SliceName = "test",
            Min = min,
            ExtensionUrls = [url]
        };
}
