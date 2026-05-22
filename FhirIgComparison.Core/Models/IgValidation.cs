namespace FhirIgComparison.Core.Models;

public sealed class IgValidation
{
    public required string FolderName { get; init; }
    public PackageLayout Layout { get; set; } = PackageLayout.Unknown;
    public bool HasPackageJson { get; init; }
    public bool HasFirelyCache { get; init; }
    public bool HasLockFile { get; init; }
    public string? PackageName { get; set; }
    public string? PackageVersion { get; set; }
    public string? PrimaryPackageId { get; set; }
    public string? PrimaryPackageVersion { get; set; }
    public List<string> Errors { get; init; } = [];
    public bool IsValid => Errors.Count == 0;
}
