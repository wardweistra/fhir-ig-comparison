using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public static class FhirReleaseDetector
{
    public static FhirRelease Detect(FhirPackageManifest manifest)
    {
        foreach (var version in manifest.FhirVersions ?? [])
        {
            var release = ParseVersionString(version);
            if (release is not null)
                return release.Value;
        }

        return FhirRelease.R4;
    }

    public static FhirRelease? ParseVersionString(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;

        if (version.StartsWith("3.", StringComparison.Ordinal)
            || version.StartsWith("1.0.", StringComparison.Ordinal)
            || version.Contains("STU3", StringComparison.OrdinalIgnoreCase))
            return FhirRelease.STU3;

        if (version.StartsWith("5.", StringComparison.Ordinal)
            || version.Contains("R5", StringComparison.OrdinalIgnoreCase))
            return FhirRelease.R5;

        if (version.StartsWith("4.3", StringComparison.Ordinal)
            || version.Contains("R4B", StringComparison.OrdinalIgnoreCase))
            return FhirRelease.R4B;

        if (version.StartsWith("4.", StringComparison.Ordinal))
            return FhirRelease.R4;

        return null;
    }
}
