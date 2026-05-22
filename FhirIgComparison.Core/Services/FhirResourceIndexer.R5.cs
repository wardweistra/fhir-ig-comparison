extern alias r5;
using FhirIgComparison.Core.Models;
using Hl7.Fhir.Model;
using R5Serialization = r5::Hl7.Fhir.Serialization;

namespace FhirIgComparison.Core.Services;

public sealed partial class FhirResourceIndexer
{
    private readonly R5Serialization.FhirJsonParser _r5Parser = new();

    private IndexedFhirResource? IndexR5(string json)
    {
        var parsed = _r5Parser.Parse<Resource>(json);
        var canonical = CanonicalUrlExtractor.GetCanonicalUrl(parsed);
        if (string.IsNullOrWhiteSpace(canonical))
            return null;

        if (parsed is StructureDefinition sd)
        {
            return new IndexedFhirResource
            {
                CanonicalUrl = canonical,
                ResourceType = parsed.TypeName ?? "StructureDefinition",
                FhirRelease = FhirRelease.R5,
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
            FhirRelease = FhirRelease.R5,
            Title = GetJsonString(json, "title"),
            Name = GetJsonString(json, "name")
        };
    }
}
