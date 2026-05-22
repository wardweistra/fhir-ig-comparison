using Hl7.Fhir.Model;

namespace FhirIgComparison.Core.Services;

public static class CanonicalUrlExtractor
{
    public static string? GetCanonicalUrl(Resource resource)
    {
        return resource switch
        {
            StructureDefinition sd => CombineUrl(sd.Url, sd.Version),
            ValueSet vs => CombineUrl(vs.Url, vs.Version),
            CodeSystem cs => CombineUrl(cs.Url, cs.Version),
            ConceptMap cm => CombineUrl(cm.Url, cm.Version),
            NamingSystem ns => ns.Url,
            CapabilityStatement cap => CombineUrl(cap.Url, cap.Version),
            SearchParameter sp => CombineUrl(sp.Url, sp.Version),
            OperationDefinition od => CombineUrl(od.Url, od.Version),
            CompartmentDefinition cd => CombineUrl(cd.Url, cd.Version),
            ImplementationGuide ig => CombineUrl(ig.Url, ig.Version),
            StructureMap sm => CombineUrl(sm.Url, sm.Version),
            MessageDefinition md => CombineUrl(md.Url, md.Version),
            ActivityDefinition ad => CombineUrl(ad.Url, ad.Version),
            PlanDefinition pd => CombineUrl(pd.Url, pd.Version),
            Questionnaire q => CombineUrl(q.Url, q.Version),
            Library lib => CombineUrl(lib.Url, lib.Version),
            Measure m => CombineUrl(m.Url, m.Version),
            GraphDefinition gd => CombineUrl(gd.Url, gd.Version),
            ExampleScenario es => CombineUrl(es.Url, es.Version),
            _ => FallbackCanonical(resource)
        };
    }

    private static string? FallbackCanonical(Resource resource)
    {
        if (!string.IsNullOrEmpty(resource.Id))
            return $"{resource.TypeName}/{resource.Id}";
        return null;
    }

    private static string? CombineUrl(string? url, string? version)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (string.IsNullOrWhiteSpace(version))
            return url;
        return url.Contains('|', StringComparison.Ordinal) ? url : $"{url}|{version}";
    }
}
