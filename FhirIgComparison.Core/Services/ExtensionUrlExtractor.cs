extern alias stu3;
using Hl7.Fhir.Model;
using Stu3 = stu3::Hl7.Fhir.Model;

namespace FhirIgComparison.Core.Services;

internal static class ExtensionUrlExtractor
{
    public static IReadOnlyList<string> FromR4(ElementDefinition element)
    {
        var urls = new List<string>();
        urls.AddRange(FromExtensionTypeProfiles(element.Type));
        if (element.Fixed is Extension fixedExt && !string.IsNullOrWhiteSpace(fixedExt.Url))
            urls.Add(fixedExt.Url);
        if (element.Pattern is Extension patternExt && !string.IsNullOrWhiteSpace(patternExt.Url))
            urls.Add(patternExt.Url);
        urls.AddRange(FromR4UrlChildFixed(element));
        return Distinct(urls);
    }

    public static IReadOnlyList<string> FromStu3(Stu3.ElementDefinition element) =>
        Distinct(FromStu3ExtensionTypeProfiles(element.Type));

    private static IEnumerable<string> FromExtensionTypeProfiles(
        IEnumerable<ElementDefinition.TypeRefComponent>? types)
    {
        if (types is null)
            yield break;

        foreach (var type in types)
        {
            if (!string.Equals(type.Code, "Extension", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var profile in type.Profile ?? [])
            {
                if (!string.IsNullOrWhiteSpace(profile))
                    yield return profile;
            }
        }
    }

    private static IEnumerable<string> FromStu3ExtensionTypeProfiles(
        IEnumerable<Stu3.ElementDefinition.TypeRefComponent>? types)
    {
        if (types is null)
            yield break;

        foreach (var type in types)
        {
            if (!string.Equals(type.Code, "Extension", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(type.Profile))
                yield return type.Profile;
        }
    }

    private static IEnumerable<string> FromR4UrlChildFixed(ElementDefinition element)
    {
        if (!element.Path.EndsWith(".url", StringComparison.Ordinal))
            yield break;

        if (element.Fixed is FhirUri uri && !string.IsNullOrWhiteSpace(uri.Value))
            yield return uri.Value;
    }

    private static IReadOnlyList<string> Distinct(IEnumerable<string> urls) =>
        urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal).ToList();
}
