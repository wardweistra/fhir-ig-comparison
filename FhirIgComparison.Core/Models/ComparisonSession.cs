namespace FhirIgComparison.Core.Models;

public sealed class ComparisonSession
{
    public string? RootFolderName { get; init; }
    public List<IgPackage> Packages { get; init; } = [];
    public List<MatchGroup> MatchGroups { get; set; } = [];
}
