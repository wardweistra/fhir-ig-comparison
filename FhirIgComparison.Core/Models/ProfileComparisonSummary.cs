namespace FhirIgComparison.Core.Models;

public sealed class ProfileComparisonSummary
{
    public IReadOnlyList<ProfileColumnSummary> Columns { get; init; } = [];
    public bool HasFhirVersionMismatch { get; init; }
}
