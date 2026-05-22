using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Web.Services;

public sealed class FolderPickResult
{
    public string? Error { get; init; }
    public string? RootName { get; init; }
    public bool WasCancelled { get; init; }
    public IReadOnlyList<FileEntry> Files { get; init; } = [];
}
