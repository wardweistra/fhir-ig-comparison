extern alias r4b;
using FhirIgComparison.Core.Models;
using Hl7.Fhir.Model;
using R4bSerialization = r4b::Hl7.Fhir.Serialization;

namespace FhirIgComparison.Core.Services;

public sealed partial class FhirResourceIndexer
{
    private readonly R4bSerialization.FhirJsonParser _r4bParser = new();

    private IndexedFhirResource? IndexR4b(string json)
    {
        var parsed = _r4bParser.Parse<Resource>(json);
        var canonical = CanonicalUrlExtractor.GetCanonicalUrl(parsed);
        if (string.IsNullOrWhiteSpace(canonical))
            return null;

        if (parsed is StructureDefinition sd)
        {
            return new IndexedFhirResource
            {
                CanonicalUrl = canonical,
                ResourceType = parsed.TypeName ?? "StructureDefinition",
                FhirRelease = FhirRelease.R4B,
                Title = sd.Title,
                Name = sd.Name,
                BaseDefinition = sd.BaseDefinition,
                FhirVersion = sd.FhirVersion?.ToString(),
                StructureDefinitionType = sd.Type,
                StructureDefinitionKind = sd.Kind?.ToString(),
                Derivation = sd.Derivation?.ToString(),
                Elements = _elementExtractor.FromStructureDefinition(sd)
            };
        }

        return new IndexedFhirResource
        {
            CanonicalUrl = canonical,
            ResourceType = parsed.TypeName ?? parsed.GetType().Name,
            FhirRelease = FhirRelease.R4B,
            Title = GetJsonString(json, "title"),
            Name = GetJsonString(json, "name")
        };
    }
}
