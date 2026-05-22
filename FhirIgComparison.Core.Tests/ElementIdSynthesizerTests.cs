using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Core.Tests;

public class ElementIdSynthesizerTests
{
    [Fact]
    public void FromPath_inserts_active_slice_after_extension_container()
    {
        var activeSlices = new Dictionary<string, string>
        {
            ["Patient.extension"] = "nationality"
        };

        var elementId = ElementIdSynthesizer.FromPath("Patient.extension.url", activeSlices);

        Assert.Equal("Patient.extension:nationality.url", elementId);
    }

    [Fact]
    public void FromPath_supports_nested_extension_slices()
    {
        var activeSlices = new Dictionary<string, string>
        {
            ["Patient.communication.extension"] = "languageControl",
            ["Patient.communication.extension.extension"] = "level"
        };

        var elementId = ElementIdSynthesizer.FromPath(
            "Patient.communication.extension.extension.url",
            activeSlices);

        Assert.Equal("Patient.communication.extension:languageControl.extension:level.url", elementId);
    }
}
