extern alias stu3;
using System.Text.Json;
using FhirIgComparison.Core.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Stu3 = stu3::Hl7.Fhir.Model;
using Stu3Serialization = stu3::Hl7.Fhir.Serialization;

namespace FhirIgComparison.Core.Services;

public sealed partial class FhirResourceIndexer
{
    private readonly FhirJsonParser _r4Parser = new();
    private readonly Stu3Serialization.FhirJsonParser _stu3Parser = new();
    private readonly SnapshotElementExtractor _elementExtractor = new();

    public bool TryIndex(string json, FhirRelease release, out IndexedFhirResource? resource)
    {
        resource = null;
        try
        {
            resource = release switch
            {
                FhirRelease.STU3 => IndexStu3(json),
                FhirRelease.R4B => IndexR4b(json),
                FhirRelease.R5 => IndexR5(json),
                _ => IndexR4(json)
            };
            return resource is not null;
        }
        catch
        {
            return false;
        }
    }

    private IndexedFhirResource? IndexR4(string json)
    {
        var parsed = _r4Parser.Parse<Resource>(json);
        var canonical = CanonicalUrlExtractor.GetCanonicalUrl(parsed);
        if (string.IsNullOrWhiteSpace(canonical))
            return null;

        if (parsed is StructureDefinition sd)
        {
            return new IndexedFhirResource
            {
                CanonicalUrl = canonical,
                ResourceType = parsed.TypeName ?? "StructureDefinition",
                FhirRelease = FhirRelease.R4,
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
            FhirRelease = FhirRelease.R4,
            Title = GetJsonString(json, "title"),
            Name = GetJsonString(json, "name")
        };
    }

    private IndexedFhirResource? IndexStu3(string json)
    {
        var parsed = _stu3Parser.Parse<Resource>(json);
        var canonical = GetStu3CanonicalUrl(parsed);
        if (string.IsNullOrWhiteSpace(canonical))
            return null;

        if (parsed is Stu3.StructureDefinition sd)
        {
            return new IndexedFhirResource
            {
                CanonicalUrl = canonical,
                ResourceType = parsed.TypeName ?? "StructureDefinition",
                FhirRelease = FhirRelease.STU3,
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
            FhirRelease = FhirRelease.STU3,
            Title = GetJsonString(json, "title"),
            Name = GetJsonString(json, "name")
        };
    }

    private static string? GetStu3CanonicalUrl(Resource resource) =>
        resource switch
        {
            Stu3.StructureDefinition sd => CombineUrl(sd.Url, sd.Version),
            Stu3.ValueSet vs => CombineUrl(vs.Url, vs.Version),
            Stu3.CodeSystem cs => CombineUrl(cs.Url, cs.Version),
            Stu3.ConceptMap cm => CombineUrl(cm.Url, cm.Version),
            Stu3.NamingSystem ns => ns.Url,
            Stu3.CapabilityStatement cap => CombineUrl(cap.Url, cap.Version),
            Stu3.SearchParameter sp => CombineUrl(sp.Url, sp.Version),
            Stu3.OperationDefinition od => CombineUrl(od.Url, od.Version),
            Stu3.CompartmentDefinition cd => cd.Url,
            Stu3.ImplementationGuide ig => CombineUrl(ig.Url, ig.Version),
            Stu3.StructureMap sm => CombineUrl(sm.Url, sm.Version),
            Stu3.MessageDefinition md => CombineUrl(md.Url, md.Version),
            Stu3.ActivityDefinition ad => CombineUrl(ad.Url, ad.Version),
            Stu3.PlanDefinition pd => CombineUrl(pd.Url, pd.Version),
            Stu3.Questionnaire q => CombineUrl(q.Url, q.Version),
            Stu3.Library lib => CombineUrl(lib.Url, lib.Version),
            Stu3.Measure m => CombineUrl(m.Url, m.Version),
            Stu3.GraphDefinition gd => CombineUrl(gd.Url, gd.Version),
            _ => string.IsNullOrEmpty(resource.Id) ? null : $"{resource.TypeName}/{resource.Id}"
        };

    private static string? CombineUrl(string? url, string? version)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (string.IsNullOrWhiteSpace(version))
            return url;
        return url.Contains('|', StringComparison.Ordinal) ? url : $"{url}|{version}";
    }

    private static string? GetJsonString(string json, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        catch
        {
            // ignored
        }
        return null;
    }
}
