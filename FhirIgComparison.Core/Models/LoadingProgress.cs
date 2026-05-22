namespace FhirIgComparison.Core.Models;

public enum LoadPhase
{
    Idle,
    ReadingFiles,
    ParsingResources,
    MatchingResources,
    Complete
}

public sealed class LoadingProgress
{
    public const int DefaultStepCount = 3;

    private const double ReadingShare = 45;
    private const double ParsingShare = 50;
    private const double MatchingShare = 5;

    public LoadPhase Phase { get; init; }
    public string Message { get; init; } = "";
    public string? IgFolder { get; init; }
    public int Current { get; init; }
    public int Total { get; init; }
    public int StepNumber { get; init; }
    public int StepCount { get; init; } = DefaultStepCount;
    public int IgIndex { get; init; }
    public int IgCount { get; init; }
    public double OverallPercent { get; init; }

    public double PhasePercent => Total > 0 ? 100.0 * Current / Total : 0;

    public static double ComputeOverallPercent(LoadPhase phase, int current, int total) => phase switch
    {
        LoadPhase.ReadingFiles when total > 0 => ReadingShare * current / total,
        LoadPhase.ParsingResources when total > 0 => ReadingShare + ParsingShare * current / total,
        LoadPhase.MatchingResources when total > 0 => ReadingShare + ParsingShare + MatchingShare * current / total,
        LoadPhase.MatchingResources => ReadingShare + ParsingShare,
        LoadPhase.Complete => 100,
        _ => 0
    };
}
