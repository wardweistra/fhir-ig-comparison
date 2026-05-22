using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public static class FirelyPackageResolver
{
    public static IReadOnlyList<PrimaryPackageRef> ResolvePrimaryPackages(
        FhirPackageManifest manifest,
        FhirPackageLock lockFile)
    {
        if (manifest.Dependencies is null || manifest.Dependencies.Count == 0)
            return [];

        var resolved = new List<PrimaryPackageRef>();
        foreach (var (packageId, _) in manifest.Dependencies)
        {
            if (lockFile.Dependencies is null
                || !lockFile.Dependencies.TryGetValue(packageId, out var version)
                || string.IsNullOrWhiteSpace(version))
                continue;

            resolved.Add(new PrimaryPackageRef(packageId, version));
        }

        return resolved;
    }

    public static string GetCacheFolderName(string packageId, string version) =>
        $"{packageId}#{version}";

    public static string GetPackagePathPrefix(string igFolder, string packageId, string version) =>
        $"{igFolder}/.fhir-package-cache/{GetCacheFolderName(packageId, version)}/package/";
}

public sealed record PrimaryPackageRef(string PackageId, string Version);
