using System.Text.Json.Serialization;
using FhirIgComparison.Core.Models;
using Microsoft.JSInterop;

namespace FhirIgComparison.Web.Services;

public sealed class FolderLoadCallbacks : IAsyncDisposable
{
    private readonly ComparisonState _state;
    private readonly List<FileEntry> _files = [];
    private DotNetObjectReference<FolderLoadCallbacks>? _reference;

    public FolderLoadCallbacks(ComparisonState state) => _state = state;

    public string? RootName { get; private set; }
    public string? Error { get; private set; }
    public bool WasCancelled { get; private set; }

    public IReadOnlyList<FileEntry> Files => _files;

    public DotNetObjectReference<FolderLoadCallbacks> CreateReference()
    {
        _reference = DotNetObjectReference.Create(this);
        return _reference;
    }

    [JSInvokable]
    public Task OnPickStarted(string rootName)
    {
        RootName = rootName;
        _files.Clear();
        _state.ReportProgress(new LoadingProgress
        {
            Phase = LoadPhase.ReadingFiles,
            Message = "Scanning folders…",
            StepNumber = 1,
            StepCount = LoadingProgress.DefaultStepCount,
            Current = 0,
            Total = 0,
            OverallPercent = 0
        });
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnProgress(
        string phase,
        string message,
        int current,
        int total,
        string? igFolder,
        int stepNumber,
        int stepCount,
        int igIndex,
        int igCount,
        double overallPercent)
    {
        if (Enum.TryParse<LoadPhase>(phase, ignoreCase: true, out var loadPhase))
        {
            _state.ReportProgress(new LoadingProgress
            {
                Phase = loadPhase,
                Message = message,
                IgFolder = string.IsNullOrEmpty(igFolder) ? null : igFolder,
                Current = current,
                Total = total,
                StepNumber = stepNumber > 0 ? stepNumber : 1,
                StepCount = stepCount > 0 ? stepCount : LoadingProgress.DefaultStepCount,
                IgIndex = igIndex,
                IgCount = igCount,
                OverallPercent = overallPercent
            });
        }
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnFilesBatch(FileDto[] batch)
    {
        foreach (var dto in batch)
        {
            _files.Add(new FileEntry
            {
                Path = dto.Path,
                Bytes = string.IsNullOrEmpty(dto.Base64)
                    ? []
                    : Convert.FromBase64String(dto.Base64)
            });
        }
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPickError(string error)
    {
        Error = error;
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPickCancelled()
    {
        WasCancelled = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _reference?.Dispose();
        return ValueTask.CompletedTask;
    }

    public sealed class FileDto
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("base64")]
        public string Base64 { get; set; } = "";
    }
}
