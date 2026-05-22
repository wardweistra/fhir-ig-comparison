using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace FhirIgComparison.Core.Services;

/// <summary>
/// Minimal Firely parse check for WASM compatibility.
/// </summary>
public static class FirelySpike
{
    private const string MinimalStructureDefinitionJson = """
        {
          "resourceType": "StructureDefinition",
          "id": "spike-test",
          "url": "http://example.org/fhir/StructureDefinition/spike-test",
          "name": "SpikeTest",
          "status": "draft",
          "kind": "resource",
          "abstract": false,
          "type": "Patient",
          "snapshot": {
            "element": [
              {
                "id": "Patient",
                "path": "Patient",
                "short": "Patient",
                "definition": "Patient resource",
                "min": 0,
                "max": "*"
              }
            ]
          }
        }
        """;

    public static string Run()
    {
        var parser = new FhirJsonParser();
        var sd = parser.Parse<StructureDefinition>(MinimalStructureDefinitionJson);
        return sd.Url ?? sd.Id ?? "ok";
    }
}
