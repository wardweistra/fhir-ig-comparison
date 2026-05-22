using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public static class ExtensionElementIdentity
{
    public static bool IsExtensionSlice(SnapshotElement element) =>
        element.Path.EndsWith(".extension", StringComparison.OrdinalIgnoreCase)
        && element.ExtensionUrls.Count > 0;

    public static string? GetPrimaryExtensionUrl(SnapshotElement element) =>
        element.ExtensionUrls.FirstOrDefault();
}
