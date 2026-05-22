namespace FhirIgComparison.Core.Models;

public sealed class ManualComparisonSelection
{
    public string ResourceType { get; set; } = "StructureDefinition";
    public string? StructureDefinitionBaseType { get; set; }
    public Dictionary<string, string> SelectedCanonicalByIg { get; } = new(StringComparer.OrdinalIgnoreCase);
}
