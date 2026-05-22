using System.Text;
using System.Text.Json;
using FhirIgComparison.Core.Models;

namespace FhirIgComparison.Core.Services;

public sealed class FolderPackageLoader
{
    private readonly FhirResourceIndexer _indexer = new();

    public ComparisonSession Load(string? rootFolderName, IReadOnlyList<FileEntry> files)
    {
        var session = new ComparisonSession { RootFolderName = rootFolderName };
        var igFolders = DetectIgFolders(files);

        foreach (var igFolder in igFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var igFiles = files
                .Where(f => f.Path.StartsWith(igFolder + "/", StringComparison.OrdinalIgnoreCase)
                            || f.Path.Equals(igFolder, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var validation = ValidateIg(igFolder, igFiles);
            var manifest = ParseManifest(igFiles, validation) ?? new FhirPackageManifest();
            var lockFile = ParseLockFile(igFiles, validation);

            ApplyPrimaryPackageDisplay(validation, manifest, lockFile, igFiles);

            var igPackage = new IgPackage
            {
                FolderName = igFolder,
                Manifest = manifest,
                Validation = validation,
                FhirRelease = FhirReleaseDetector.Detect(manifest)
            };

            if (validation.IsValid)
                LoadResources(igPackage, igFiles, lockFile);

            session.Packages.Add(igPackage);
        }

        return session;
    }

    public async Task<ComparisonSession> LoadAsync(
        string? rootFolderName,
        IReadOnlyList<FileEntry> files,
        IProgress<LoadingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        const int parseBatchSize = 25;
        var session = new ComparisonSession { RootFolderName = rootFolderName };
        var igFolders = DetectIgFolders(files).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        var allArtifactPaths = new List<(string IgFolder, IgPackage Package, List<string> Paths, FhirPackageLock? Lock)>();

        foreach (var igFolder in igFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var igFiles = files
                .Where(f => f.Path.StartsWith(igFolder + "/", StringComparison.OrdinalIgnoreCase)
                            || f.Path.Equals(igFolder, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var validation = ValidateIg(igFolder, igFiles);
            var manifest = ParseManifest(igFiles, validation) ?? new FhirPackageManifest();
            var lockFile = ParseLockFile(igFiles, validation);

            ApplyPrimaryPackageDisplay(validation, manifest, lockFile, igFiles);

            var igPackage = new IgPackage
            {
                FolderName = igFolder,
                Manifest = manifest,
                Validation = validation,
                FhirRelease = FhirReleaseDetector.Detect(manifest)
            };

            session.Packages.Add(igPackage);

            if (validation.IsValid)
            {
                var paths = CollectArtifactPaths(igPackage, igFiles, lockFile).ToList();
                allArtifactPaths.Add((igFolder, igPackage, paths, lockFile));
            }
        }

        var totalToParse = allArtifactPaths.Sum(x => x.Paths.Count);
        var parsed = 0;
        var igCount = allArtifactPaths.Count;

        progress?.Report(new LoadingProgress
        {
            Phase = LoadPhase.ParsingResources,
            Message = "Parsing resources",
            StepNumber = 2,
            StepCount = LoadingProgress.DefaultStepCount,
            IgCount = igCount,
            Current = 0,
            Total = totalToParse,
            OverallPercent = LoadingProgress.ComputeOverallPercent(LoadPhase.ParsingResources, 0, totalToParse)
        });
        await System.Threading.Tasks.Task.Yield();

        for (var igIndex = 0; igIndex < allArtifactPaths.Count; igIndex++)
        {
            var (igFolder, igPackage, paths, _) = allArtifactPaths[igIndex];
            var seenCanonicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batchCount = 0;

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryAddResource(igPackage, path, files, seenCanonicals);
                parsed++;
                batchCount++;

                if (batchCount >= parseBatchSize)
                {
                    batchCount = 0;
                    progress?.Report(new LoadingProgress
                    {
                        Phase = LoadPhase.ParsingResources,
                        Message = "Parsing resources",
                        IgFolder = igFolder,
                        IgIndex = igIndex + 1,
                        IgCount = igCount,
                        StepNumber = 2,
                        StepCount = LoadingProgress.DefaultStepCount,
                        Current = parsed,
                        Total = totalToParse,
                        OverallPercent = LoadingProgress.ComputeOverallPercent(
                            LoadPhase.ParsingResources, parsed, totalToParse)
                    });
                    await System.Threading.Tasks.Task.Yield();
                }
            }

            progress?.Report(new LoadingProgress
            {
                Phase = LoadPhase.ParsingResources,
                Message = "Parsing resources",
                IgFolder = igFolder,
                IgIndex = igIndex + 1,
                IgCount = igCount,
                StepNumber = 2,
                StepCount = LoadingProgress.DefaultStepCount,
                Current = parsed,
                Total = totalToParse,
                OverallPercent = LoadingProgress.ComputeOverallPercent(
                    LoadPhase.ParsingResources, parsed, totalToParse)
            });
            await System.Threading.Tasks.Task.Yield();
        }

        return session;
    }

    private static HashSet<string> DetectIgFolders(IReadOnlyList<FileEntry> files)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var slash = file.Path.IndexOf('/');
            if (slash <= 0)
                continue;
            folders.Add(file.Path[..slash]);
        }
        return folders;
    }

    private static PackageLayout DetectLayout(string folderName, IReadOnlyList<FileEntry> igFiles)
    {
        var hasFirelyCache = igFiles.Any(f =>
            f.Path.StartsWith($"{folderName}/.fhir-package-cache/", StringComparison.OrdinalIgnoreCase));
        var hasLockFile = igFiles.Any(f =>
            f.Path.Equals($"{folderName}/fhirpkg.lock.json", StringComparison.OrdinalIgnoreCase));

        if (hasFirelyCache || hasLockFile)
            return PackageLayout.FirelyTerminal;
        return PackageLayout.Unknown;
    }

    private static IgValidation ValidateIg(string folderName, IReadOnlyList<FileEntry> igFiles)
    {
        var layout = DetectLayout(folderName, igFiles);
        var validation = new IgValidation
        {
            FolderName = folderName,
            Layout = layout,
            HasPackageJson = igFiles.Any(f =>
                f.Path.Equals($"{folderName}/package.json", StringComparison.OrdinalIgnoreCase)),
            HasFirelyCache = igFiles.Any(f =>
                f.Path.StartsWith($"{folderName}/.fhir-package-cache/", StringComparison.OrdinalIgnoreCase)),
            HasLockFile = igFiles.Any(f =>
                f.Path.Equals($"{folderName}/fhirpkg.lock.json", StringComparison.OrdinalIgnoreCase))
        };

        if (!validation.HasPackageJson)
            validation.Errors.Add("Missing package.json");
        if (!validation.HasLockFile)
            validation.Errors.Add("Missing fhirpkg.lock.json — run fhir restore in this folder");
        if (!validation.HasFirelyCache)
            validation.Errors.Add("Missing .fhir-package-cache/ — run fhir cache use-local and fhir restore");

        return validation;
    }

    private FhirPackageManifest? ParseManifest(IReadOnlyList<FileEntry> igFiles, IgValidation validation)
    {
        var manifestFile = igFiles.FirstOrDefault(f =>
            f.Path.Equals($"{validation.FolderName}/package.json", StringComparison.OrdinalIgnoreCase));

        if (manifestFile is null)
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(manifestFile.Bytes);
            var manifest = JsonSerializer.Deserialize<FhirPackageManifest>(json);
            if (manifest is not null
                && (manifest.Dependencies is null || manifest.Dependencies.Count == 0))
                validation.Errors.Add("package.json has no dependencies — add the IG package to compare");
            return manifest;
        }
        catch (Exception ex)
        {
            validation.Errors.Add($"Invalid package.json: {ex.Message}");
            return null;
        }
    }

    private static FhirPackageLock? ParseLockFile(IReadOnlyList<FileEntry> igFiles, IgValidation validation)
    {
        if (validation.Layout != PackageLayout.FirelyTerminal)
            return null;

        var lockFile = igFiles.FirstOrDefault(f =>
            f.Path.Equals($"{validation.FolderName}/fhirpkg.lock.json", StringComparison.OrdinalIgnoreCase));

        if (lockFile is null)
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(lockFile.Bytes);
            return JsonSerializer.Deserialize<FhirPackageLock>(json);
        }
        catch (Exception ex)
        {
            validation.Errors.Add($"Invalid fhirpkg.lock.json: {ex.Message}");
            return null;
        }
    }

    private static void ApplyPrimaryPackageDisplay(
        IgValidation validation,
        FhirPackageManifest manifest,
        FhirPackageLock? lockFile,
        IReadOnlyList<FileEntry> igFiles)
    {
        if (validation.Layout != PackageLayout.FirelyTerminal || lockFile is null)
            return;

        var primaryPackages = FirelyPackageResolver.ResolvePrimaryPackages(manifest, lockFile);
        if (primaryPackages.Count == 0)
        {
            validation.Errors.Add("No primary package found in fhirpkg.lock.json for package.json dependencies");
            return;
        }

        var first = primaryPackages[0];
        validation.PrimaryPackageId = first.PackageId;
        validation.PrimaryPackageVersion = first.Version;
        validation.PackageName = first.PackageId;
        validation.PackageVersion = first.Version;

        foreach (var primary in primaryPackages)
        {
            var prefix = FirelyPackageResolver.GetPackagePathPrefix(
                validation.FolderName, primary.PackageId, primary.Version);
            var hasArtifacts = igFiles.Any(f =>
                f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && IsArtifactPath(f.Path));
            if (!hasArtifacts)
            {
                validation.Errors.Add(
                    $"Primary package not found in cache: {primary.PackageId}#{primary.Version}");
            }
        }
    }

    private void LoadResources(IgPackage igPackage, IReadOnlyList<FileEntry> igFiles, FhirPackageLock? lockFile)
    {
        var artifactPaths = CollectArtifactPaths(igPackage, igFiles, lockFile);
        var seenCanonicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in artifactPaths)
            TryAddResource(igPackage, path, igFiles, seenCanonicals);
    }

    private static IEnumerable<string> CollectArtifactPaths(
        IgPackage igPackage,
        IReadOnlyList<FileEntry> igFiles,
        FhirPackageLock? lockFile)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var igFolder = igPackage.FolderName;

        if (igPackage.Validation.Layout == PackageLayout.FirelyTerminal && lockFile is not null)
        {
            var primaryPackages = FirelyPackageResolver.ResolvePrimaryPackages(igPackage.Manifest, lockFile);
            foreach (var primary in primaryPackages)
            {
                var prefix = FirelyPackageResolver.GetPackagePathPrefix(
                    igFolder, primary.PackageId, primary.Version);

                foreach (var file in igFiles)
                {
                    if (!IsArtifactPath(file.Path))
                        continue;
                    if (!file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    paths.Add(file.Path);
                }
            }

            return paths;
        }

        return paths;
    }

    private static bool IsArtifactPath(string path)
    {
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.EndsWith("/.index.json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/.firely.index.json", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Contains("/other/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/openapi/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/xml/", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private void TryAddResource(
        IgPackage igPackage,
        string path,
        IReadOnlyList<FileEntry> igFiles,
        HashSet<string> seenCanonicals)
    {
        var file = igFiles.FirstOrDefault(f =>
            f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return;

        var json = Encoding.UTF8.GetString(file.Bytes);
        if (!_indexer.TryIndex(json, igPackage.FhirRelease, out var resource) || resource is null)
            return;

        if (!seenCanonicals.Add(resource.CanonicalUrl))
            return;

        igPackage.ResourcesByCanonical[resource.CanonicalUrl] = resource;
    }
}
