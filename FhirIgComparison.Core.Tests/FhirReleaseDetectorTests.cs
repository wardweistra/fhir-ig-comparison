using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class FhirReleaseDetectorTests
{
    [Theory]
    [InlineData("3.0.2", FhirRelease.STU3)]
    [InlineData("4.0.1", FhirRelease.R4)]
    [InlineData("4.2.0", FhirRelease.R4)]
    [InlineData("4.3.0", FhirRelease.R4B)]
    [InlineData("5.0.0", FhirRelease.R5)]
    public void ParseVersionString_maps_fhir_versions(string version, FhirRelease expected)
    {
        Assert.Equal(expected, FhirReleaseDetector.ParseVersionString(version));
    }

    [Fact]
    public void Detect_uses_manifest_fhirVersions()
    {
        var manifest = new FhirPackageManifest { FhirVersions = ["3.0.2"] };
        Assert.Equal(FhirRelease.STU3, FhirReleaseDetector.Detect(manifest));
    }

    [Fact]
    public void Detect_maps_r4b_and_r5_manifests()
    {
        Assert.Equal(FhirRelease.R4B, FhirReleaseDetector.Detect(new FhirPackageManifest { FhirVersions = ["4.3.0"] }));
        Assert.Equal(FhirRelease.R5, FhirReleaseDetector.Detect(new FhirPackageManifest { FhirVersions = ["5.0.0"] }));
    }
}
