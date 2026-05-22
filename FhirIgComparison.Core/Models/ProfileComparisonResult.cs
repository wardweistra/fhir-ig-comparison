namespace FhirIgComparison.Core.Models;

public sealed class ProfileComparisonResult
{
    public required ProfileComparisonSummary Summary { get; init; }
    public required IReadOnlyList<string> IgOrder { get; init; }
    public IReadOnlyDictionary<string, string?> PackageScopeByIg { get; init; }
        = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public required IReadOnlyList<ElementComparisonRow> Rows { get; init; }
}
