using System.Text.Json.Serialization;

namespace FhirIgComparison.Core.Models;

public sealed class FhirPackageLock
{
    [JsonPropertyName("updated")]
    public string? Updated { get; set; }

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; set; }

    [JsonPropertyName("missing")]
    public Dictionary<string, string>? Missing { get; set; }
}
