namespace FhirIgComparison.Core.Models;

public sealed class ProfileColumnSummary
{
    public required string IgFolderName { get; init; }
    public required string ProfileName { get; init; }
    public required string CanonicalUrl { get; init; }
    public string? PackageScope { get; init; }
    public string? BaseDefinition { get; init; }
    public string? FhirVersion { get; init; }
    public int ConstrainedElementCount { get; init; }
    public int MustSupportCount { get; init; }
    public int ExtensionCount { get; init; }
}
