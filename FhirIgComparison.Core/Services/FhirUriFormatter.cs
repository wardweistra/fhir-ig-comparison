extern alias stu3;
using Hl7.Fhir.Model;
using Stu3 = stu3::Hl7.Fhir.Model;

namespace FhirIgComparison.Core.Services;

internal static class FhirUriFormatter
{
    public static string? FormatBindingValueSet(ElementDefinition.ElementDefinitionBindingComponent? binding) =>
        string.IsNullOrWhiteSpace(binding?.ValueSet) ? null : binding.ValueSet;

    public static string? FormatBindingValueSet(Stu3.ElementDefinition.ElementDefinitionBindingComponent? binding) =>
        FormatStu3Reference(binding?.ValueSet);

    /// <summary>
    /// STU3 binding.valueSet is a Reference (ResourceReference); ToString() yields the CLR type name.
    /// </summary>
    private static string? FormatStu3Reference(object? reference)
    {
        if (reference is null)
            return null;

        if (reference.GetType().GetProperty("Reference")?.GetValue(reference) is string url
            && !string.IsNullOrWhiteSpace(url))
            return url;

        return null;
    }
}
