using System.Text.Json.Serialization;
using FhirIgComparison.Core.Models;
using Microsoft.JSInterop;

namespace FhirIgComparison.Web.Services;

public sealed class FolderAccessService(IJSRuntime jsRuntime)
{
    private IJSObjectReference? _module;

    public async Task<FolderPickResult> PickComparisonFolderWithProgressAsync(
        ComparisonState state,
        CancellationToken cancellationToken = default)
    {
        await using var callbacks = new FolderLoadCallbacks(state);
        var dotNetRef = callbacks.CreateReference();
        var module = await GetModuleAsync();

        await module.InvokeVoidAsync("pickComparisonFolderWithProgress", cancellationToken, dotNetRef);

        return new FolderPickResult
        {
            WasCancelled = callbacks.WasCancelled,
            Error = callbacks.Error,
            RootName = callbacks.RootName,
            Files = callbacks.Files
        };
    }

    public async Task<LegacyFolderPickResult> PickComparisonFolderAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<LegacyFolderPickResult>("pickComparisonFolder");
    }

    public List<FileEntry> ToFileEntries(LegacyFolderPickResult result)
    {
        return result.Files
            .Select(f => new FileEntry
            {
                Path = f.Path,
                Bytes = string.IsNullOrEmpty(f.Base64)
                    ? []
                    : Convert.FromBase64String(f.Base64)
            })
            .ToList();
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        if (_module is null)
            _module = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/folderAccess.js");
        return _module;
    }

    public sealed class LegacyFolderPickResult
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("rootName")]
        public string? RootName { get; set; }

        [JsonPropertyName("files")]
        public List<LegacyFileDto> Files { get; set; } = [];
    }

    public sealed class LegacyFileDto
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("base64")]
        public string Base64 { get; set; } = "";
    }
}
