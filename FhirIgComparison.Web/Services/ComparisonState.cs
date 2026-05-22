using FhirIgComparison.Core.Models;
using FhirIgComparison.Core.Services;

namespace FhirIgComparison.Web.Services;

public sealed class ComparisonState : IProgress<LoadingProgress>
{
    public ComparisonSession? Session { get; private set; }
    public ResourceCatalog? Catalog { get; private set; }
    public ComparisonMode Mode { get; private set; } = ComparisonMode.ByResourceSelection;
    public ManualComparisonSelection ManualSelection { get; } = new();
    public MatchGroup? SelectedMatch { get; private set; }
    public ProfileComparisonResult? Comparison { get; private set; }
    public int ComparisonRevision { get; private set; }
    public ComparisonDimension ActiveDimension { get; private set; } = ComparisonDimension.Cardinality;
    public ComparisonRowFilter RowFilter { get; private set; } = ComparisonRowFilter.DifferencesOnly;
    public string? StatusMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ManualComparisonMessage { get; private set; }
    public bool IsLoading { get; private set; }
    public LoadingProgress? Progress { get; private set; }

    public event Action? OnChange;

    private readonly FolderPackageLoader _loader = new();
    private readonly ResourceMatcher _matcher = new();
    private readonly StructureDefinitionComparer _comparer = new();
    private readonly ManualComparisonBuilder _manualBuilder = new();

    public string FirelySpikeResult { get; } = FirelySpike.Run();

    public void SetLoading(bool loading)
    {
        IsLoading = loading;
        if (!loading)
            Progress = null;
        Notify();
    }

    public void ReportProgress(LoadingProgress progress)
    {
        Progress = progress;
        Notify();
    }

    void IProgress<LoadingProgress>.Report(LoadingProgress value) => ReportProgress(value);

    public void SetError(string? message)
    {
        ErrorMessage = message;
        Notify();
    }

    public void SetActiveDimension(ComparisonDimension dimension)
    {
        ActiveDimension = dimension;
        Notify();
    }

    public void SetRowFilter(ComparisonRowFilter filter)
    {
        RowFilter = filter;
        Notify();
    }

    public void SetMode(ComparisonMode mode)
    {
        Mode = mode;
        ClearSelection();
        ManualComparisonMessage = null;
        if (mode == ComparisonMode.ByResourceSelection && Catalog is not null)
        {
            InitializeManualSelectionDefaults();
        }
        Notify();
    }

    public void InitializeManualSelectionDefaults()
    {
        if (Catalog is null)
            return;

        var types = Catalog.GetResourceTypes();
        ManualSelection.ResourceType = types.FirstOrDefault(t =>
            t.Equals("StructureDefinition", StringComparison.OrdinalIgnoreCase))
            ?? types.FirstOrDefault()
            ?? "StructureDefinition";

        if (ManualSelection.ResourceType.Equals("StructureDefinition", StringComparison.OrdinalIgnoreCase))
        {
            var baseTypes = Catalog.GetStructureDefinitionBaseTypes();
            ManualSelection.StructureDefinitionBaseType = baseTypes.FirstOrDefault(t =>
                t.Equals("Patient", StringComparison.OrdinalIgnoreCase))
                ?? baseTypes.FirstOrDefault();
        }
        else
        {
            ManualSelection.StructureDefinitionBaseType = null;
        }

        Catalog.ApplySuggestedDefaults(ManualSelection);
    }

    public void OnManualResourceTypeChanged(string resourceType)
    {
        ManualSelection.ResourceType = resourceType;
        if (resourceType.Equals("StructureDefinition", StringComparison.OrdinalIgnoreCase))
        {
            ManualSelection.StructureDefinitionBaseType = Catalog?.GetStructureDefinitionBaseTypes()
                .FirstOrDefault();
        }
        else
        {
            ManualSelection.StructureDefinitionBaseType = null;
        }

        Catalog?.ApplySuggestedDefaults(ManualSelection);
        ClearSelection();
        ManualComparisonMessage = null;
        Notify();
    }

    public void OnManualBaseTypeChanged(string? baseType)
    {
        ManualSelection.StructureDefinitionBaseType = baseType;
        Catalog?.ApplySuggestedDefaults(ManualSelection);
        ClearSelection();
        ManualComparisonMessage = null;
        Notify();
    }

    public void SetManualSelection(string igFolder, string? canonicalUrl)
    {
        if (string.IsNullOrWhiteSpace(canonicalUrl))
            ManualSelection.SelectedCanonicalByIg.Remove(igFolder);
        else
            ManualSelection.SelectedCanonicalByIg[igFolder] = canonicalUrl;
        ClearSelection();
        ManualComparisonMessage = null;
        Notify();
    }

    public bool CanCompareManualSelection =>
        ManualSelection.ResourceType.Equals("StructureDefinition", StringComparison.OrdinalIgnoreCase)
        && Catalog is not null
        && CountManualSelections() >= 2;

    public int CountManualSelections() =>
        ManualSelection.SelectedCanonicalByIg.Values.Count(url => !string.IsNullOrWhiteSpace(url));

    public void CompareManualSelection()
    {
        ManualComparisonMessage = null;
        if (Session is null || Catalog is null)
            return;

        if (!ManualSelection.ResourceType.Equals("StructureDefinition", StringComparison.OrdinalIgnoreCase))
        {
            ManualComparisonMessage = "Element-level diff is only available for StructureDefinition.";
            ClearSelection();
            Notify();
            return;
        }

        var result = _manualBuilder.Build(ManualSelection, Session.Packages);
        if (!result.Success || result.Group is null)
        {
            ManualComparisonMessage = result.Error ?? "Could not build comparison.";
            ClearSelection();
            Notify();
            return;
        }

        RunComparison(result.Group);
    }

    public void LoadSession(string? rootName, IReadOnlyList<FileEntry> files)
    {
        Session = _loader.Load(rootName, files);
        FinishSessionLoad();
    }

    public async Task LoadSessionAsync(
        string? rootName,
        IReadOnlyList<FileEntry> files,
        CancellationToken cancellationToken = default)
    {
        Session = await _loader.LoadAsync(rootName, files, this, cancellationToken);

        ReportProgress(new LoadingProgress
        {
            Phase = LoadPhase.MatchingResources,
            Message = Session is null
                ? "Matching resources across IGs"
                : $"Matching {Session.Packages.Count} IG folders",
            StepNumber = 3,
            StepCount = LoadingProgress.DefaultStepCount,
            IgCount = Session?.Packages.Count ?? 0,
            OverallPercent = LoadingProgress.ComputeOverallPercent(LoadPhase.MatchingResources, 0, 1)
        });

        await System.Threading.Tasks.Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        FinishSessionLoad();

        ReportProgress(new LoadingProgress
        {
            Phase = LoadPhase.Complete,
            Message = "Done",
            StepNumber = 3,
            StepCount = LoadingProgress.DefaultStepCount,
            Current = 1,
            Total = 1,
            OverallPercent = 100
        });
    }

    private void FinishSessionLoad()
    {
        if (Session is null)
            return;

        Session.MatchGroups = _matcher.Match(Session.Packages);
        Catalog = new ResourceCatalog(Session.Packages);
        Mode = ComparisonMode.ByResourceSelection;
        ManualSelection.SelectedCanonicalByIg.Clear();
        InitializeManualSelectionDefaults();
        SelectedMatch = null;
        Comparison = null;
        ComparisonRevision = 0;
        ActiveDimension = ComparisonDimension.Cardinality;
        RowFilter = ComparisonRowFilter.DifferencesOnly;
        ManualComparisonMessage = null;
        StatusMessage = $"Loaded {Session.Packages.Count} IG folder(s), {Session.MatchGroups.Count} unique canonical URL(s).";
        ErrorMessage = null;
        Notify();
    }

    public void SelectMatch(MatchGroup group)
    {
        SelectedMatch = group;
        ManualComparisonMessage = null;
        RunComparison(group);
    }

    private void RunComparison(MatchGroup group)
    {
        if (Session is null)
        {
            Comparison = null;
            Notify();
            return;
        }

        var igOrder = Session.Packages
            .Where(p => p.Validation.IsValid && group.ResourcesByIg.ContainsKey(p.FolderName))
            .Select(p => p.FolderName)
            .ToList();

        Comparison = _comparer.Compare(group, igOrder, Session.Packages);
        ComparisonRevision++;
        ActiveDimension = ComparisonDimension.Cardinality;
        RowFilter = ComparisonRowFilter.DifferencesOnly;
        Notify();
    }

    public void ClearSelection()
    {
        SelectedMatch = null;
        Comparison = null;
        ComparisonRevision++;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
