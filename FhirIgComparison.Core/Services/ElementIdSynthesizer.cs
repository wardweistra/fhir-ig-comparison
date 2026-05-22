namespace FhirIgComparison.Core.Services;

public static class ElementIdSynthesizer
{
    public static string FromPath(string path, IReadOnlyDictionary<string, string> activeSlices)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        var parts = path.Split('.');
        var result = new System.Text.StringBuilder();
        var currentPath = "";

        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                result.Append('.');

            currentPath = i == 0 ? parts[i] : $"{currentPath}.{parts[i]}";
            result.Append(parts[i]);

            if (activeSlices.TryGetValue(currentPath, out var slice))
                result.Append(':').Append(slice);
        }

        return result.ToString();
    }
}
