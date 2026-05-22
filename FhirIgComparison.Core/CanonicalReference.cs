namespace FhirIgComparison.Core;

public static class CanonicalReference
{
    public static (string BaseUrl, string? Version) Parse(string canonical)
    {
        if (string.IsNullOrWhiteSpace(canonical))
            return (canonical ?? string.Empty, null);

        var separatorIndex = canonical.LastIndexOf('|');
        if (separatorIndex < 0)
            return (canonical, null);

        return (canonical[..separatorIndex], canonical[(separatorIndex + 1)..]);
    }

    public static bool AreBindingsCompatible(IEnumerable<string> urls)
    {
        var parsed = urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(Parse)
            .ToList();

        if (parsed.Count <= 1)
            return true;

        var distinctBases = parsed
            .Select(p => p.BaseUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinctBases.Count > 1)
            return false;

        var distinctVersions = parsed
            .Select(p => p.Version)
            .Where(v => v is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return distinctVersions.Count <= 1;
    }

    public static string GetCompatDisplayUrl(IEnumerable<string> urls)
    {
        var parsed = urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(Parse)
            .ToList();

        if (parsed.Count == 0)
            return string.Empty;

        var baseUrl = parsed[0].BaseUrl;
        var version = parsed
            .Select(p => p.Version)
            .FirstOrDefault(v => v is not null);

        return version is null ? baseUrl : $"{baseUrl}|{version}";
    }
}
