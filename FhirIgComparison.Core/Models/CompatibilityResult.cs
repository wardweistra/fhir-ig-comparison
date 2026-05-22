namespace FhirIgComparison.Core.Models;

public sealed class CompatibilityResult
{
    public string CombinedValue { get; init; } = "—";
    public CompatibilityStatus Status { get; init; } = CompatibilityStatus.NotApplicable;
    public string? Explanation { get; init; }
}
