using System.Text.Json.Serialization;

namespace FhirIgComparison.Core.Models;

public sealed class FhirPackageManifest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("fhirVersions")]
    public List<string>? FhirVersions { get; set; }

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; set; }
}
