using System.Text.RegularExpressions;
using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core;

public static class SimplifierUrls
{
    public static string? GetPackageScope(IgPackage? package)
    {
        if (package is null)
            return null;

        var validation = package.Validation;
        if (validation.Layout == PackageLayout.FirelyTerminal
            && !string.IsNullOrEmpty(validation.PrimaryPackageId))
        {
            var id = validation.PrimaryPackageId;
            var version = validation.PrimaryPackageVersion;
            return string.IsNullOrEmpty(version) ? id : $"{id}@{version}";
        }

        var name = validation.PackageName ?? package.Manifest.Name;
        if (string.IsNullOrEmpty(name))
            return null;

        var ver = validation.PackageVersion ?? package.Manifest.Version;
        return string.IsNullOrEmpty(ver) ? name : $"{name}@{ver}";
    }

    public static string? GetPackageUrl(string packageId, string? version) =>
        string.IsNullOrEmpty(packageId)
            ? null
            : string.IsNullOrEmpty(version)
                ? $"https://simplifier.net/packages/{packageId}"
                : $"https://simplifier.net/packages/{packageId}/{version}";

    public static string? GetResolveUrl(string? packageScope, string? canonical)
    {
        if (string.IsNullOrEmpty(packageScope)
            || string.IsNullOrEmpty(canonical)
            || canonical == "—")
            return null;

        return $"https://simplifier.net/resolve?scope={Uri.EscapeDataString(packageScope)}&canonical={Uri.EscapeDataString(canonical)}";
    }

    private static readonly Regex CanonicalUrlInTextRegex = new(
        @"https?://[^\s|,)|\]]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TextSegment> SplitCanonicalUrls(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [new TextSegment("—", false)];

        if (text == "—")
            return [new TextSegment("—", false)];

        var matches = CanonicalUrlInTextRegex.Matches(text);
        if (matches.Count == 0)
            return [new TextSegment(text, false)];

        var segments = new List<TextSegment>();
        var pos = 0;
        foreach (Match match in matches)
        {
            if (match.Index > pos)
                segments.Add(new TextSegment(text.Substring(pos, match.Index - pos), false));

            segments.Add(new TextSegment(match.Value, true));
            pos = match.Index + match.Length;
        }

        if (pos < text.Length)
            segments.Add(new TextSegment(text[pos..], false));

        return segments;
    }

    public readonly record struct TextSegment(string Text, bool IsCanonicalUrl);
}
