const BATCH_SIZE = 20;
const STEP_COUNT = 3;
const READING_STEP = 1;

function computeOverallReading(current, total) {
    if (total <= 0) {
        return 0;
    }
    return (current / total) * 45;
}

async function reportProgress(dotNetRef, phase, message, current, total, igFolder, stepNumber, igIndex, igCount, overallPercent) {
    await dotNetRef.invokeMethodAsync(
        'OnProgress',
        phase,
        message,
        current,
        total,
        igFolder ?? '',
        stepNumber,
        STEP_COUNT,
        igIndex ?? 0,
        igCount ?? 0,
        overallPercent);
}

export async function pickComparisonFolderWithProgress(dotNetRef) {
    if (!window.showDirectoryPicker) {
        await dotNetRef.invokeMethodAsync('OnPickError',
            'File System Access API is not supported in this browser. Use Chrome or Edge.');
        return;
    }

    try {
        const root = await window.showDirectoryPicker({ mode: 'read' });
        await dotNetRef.invokeMethodAsync('OnPickStarted', root.name);

        const igEntries = [];
        for await (const entry of root.values()) {
            if (entry.kind === 'directory') {
                igEntries.push(entry);
            }
        }

        const igCount = igEntries.length;

        await reportProgress(
            dotNetRef,
            'ReadingFiles',
            'Counting files…',
            0,
            0,
            '',
            READING_STEP,
            0,
            igCount,
            0);

        let totalFiles = 0;
        for (const igEntry of igEntries) {
            totalFiles += await countIgFiles(igEntry, igEntry.name);
        }

        await reportProgress(
            dotNetRef,
            'ReadingFiles',
            'Reading package files',
            0,
            totalFiles,
            '',
            READING_STEP,
            0,
            igCount,
            0);

        let filesRead = 0;
        let batch = [];

        for (let igIndex = 0; igIndex < igEntries.length; igIndex++) {
            const igEntry = igEntries[igIndex];
            const igName = igEntry.name;
            await readIgFolderWithProgress(
                igEntry, igName, batch, dotNetRef,
                () => filesRead, (n) => { filesRead = n; },
                totalFiles, igIndex + 1, igCount);
        }

        if (batch.length > 0) {
            await flushBatch(batch, dotNetRef);
        }

        await reportProgress(
            dotNetRef,
            'ReadingFiles',
            'Finished reading files',
            totalFiles,
            totalFiles,
            '',
            READING_STEP,
            0,
            igCount,
            computeOverallReading(totalFiles, totalFiles));
    } catch (err) {
        if (err?.name === 'AbortError') {
            await dotNetRef.invokeMethodAsync('OnPickCancelled');
            return;
        }
        await dotNetRef.invokeMethodAsync('OnPickError', err?.message ?? String(err));
    }
}

/** @deprecated Use pickComparisonFolderWithProgress */
export async function pickComparisonFolder() {
    if (!window.showDirectoryPicker) {
        return {
            error: 'File System Access API is not supported in this browser. Use Chrome or Edge.',
            rootName: null,
            files: []
        };
    }

    try {
        const root = await window.showDirectoryPicker({ mode: 'read' });
        const files = [];

        for await (const entry of root.values()) {
            if (entry.kind !== 'directory') {
                continue;
            }
            await readIgFolderLegacy(entry, entry.name, files);
        }

        return { error: null, rootName: root.name, files };
    } catch (err) {
        if (err?.name === 'AbortError') {
            return { error: null, rootName: null, files: [] };
        }
        return { error: err?.message ?? String(err), rootName: null, files: [] };
    }
}

async function readIgFolderWithProgress(
    igHandle, igName, batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igIndex, igCount) {
    let manifest = null;
    let lockFile = null;

    try {
        const pkgJsonHandle = await igHandle.getFileHandle('package.json');
        const pkgJsonFile = await pkgJsonHandle.getFile();
        const pkgJsonBytes = await readFileAsBase64(pkgJsonFile);
        await addToBatch({ path: `${igName}/package.json`, base64: pkgJsonBytes }, batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igName, igIndex, igCount);
        manifest = JSON.parse(await pkgJsonFile.text());
    } catch {
        // validation will report missing package.json
    }

    try {
        const cacheDir = await igHandle.getDirectoryHandle('.fhir-package-cache');
        await addToBatch({ path: `${igName}/.fhir-package-cache/.folder-marker`, base64: '' }, batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igName, igIndex, igCount);

        try {
            const lockHandle = await igHandle.getFileHandle('fhirpkg.lock.json');
            const lockFileContent = await lockHandle.getFile();
            const lockBytes = await readFileAsBase64(lockFileContent);
            await addToBatch({ path: `${igName}/fhirpkg.lock.json`, base64: lockBytes }, batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igName, igIndex, igCount);
            lockFile = JSON.parse(await lockFileContent.text());
        } catch {
            // validation will report missing lock file
        }

        if (manifest?.dependencies && lockFile?.dependencies) {
            for (const packageId of Object.keys(manifest.dependencies)) {
                const version = lockFile.dependencies[packageId];
                if (!version) {
                    continue;
                }
                const cacheFolderName = `${packageId}#${version}`;
                try {
                    const packageDir = await cacheDir.getDirectoryHandle(cacheFolderName);
                    const artifactsDir = await packageDir.getDirectoryHandle('package');
                    await readPackageArtifactsWithProgress(
                        artifactsDir, `${igName}/.fhir-package-cache/${cacheFolderName}/package`,
                        batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igName, igIndex, igCount);
                } catch {
                    // validation will report missing primary package in cache
                }
            }
        }
    } catch {
        // validation will report missing .fhir-package-cache/
    }
}

async function readIgFolderLegacy(igHandle, igName, files) {
    let manifest = null;
    let lockFile = null;

    try {
        const pkgJsonHandle = await igHandle.getFileHandle('package.json');
        const pkgJsonFile = await pkgJsonHandle.getFile();
        const pkgJsonBytes = await readFileAsBase64(pkgJsonFile);
        files.push({ path: `${igName}/package.json`, base64: pkgJsonBytes });
        manifest = JSON.parse(await pkgJsonFile.text());
    } catch { }

    try {
        const cacheDir = await igHandle.getDirectoryHandle('.fhir-package-cache');
        files.push({ path: `${igName}/.fhir-package-cache/.folder-marker`, base64: '' });

        try {
            const lockHandle = await igHandle.getFileHandle('fhirpkg.lock.json');
            const lockFileContent = await lockHandle.getFile();
            const lockBytes = await readFileAsBase64(lockFileContent);
            files.push({ path: `${igName}/fhirpkg.lock.json`, base64: lockBytes });
            lockFile = JSON.parse(await lockFileContent.text());
        } catch { }

        if (manifest?.dependencies && lockFile?.dependencies) {
            for (const packageId of Object.keys(manifest.dependencies)) {
                const version = lockFile.dependencies[packageId];
                if (!version) continue;
                const cacheFolderName = `${packageId}#${version}`;
                try {
                    const packageDir = await cacheDir.getDirectoryHandle(cacheFolderName);
                    const artifactsDir = await packageDir.getDirectoryHandle('package');
                    await readPackageArtifactsLegacy(artifactsDir, `${igName}/.fhir-package-cache/${cacheFolderName}/package`, files);
                } catch { }
            }
        }
    } catch { }
}

async function countIgFiles(igHandle, igName) {
    let manifest = null;
    let lockFile = null;
    let count = 0;

    try {
        await igHandle.getFileHandle('package.json');
        count++;
        const pkgJsonFile = await (await igHandle.getFileHandle('package.json')).getFile();
        manifest = JSON.parse(await pkgJsonFile.text());
    } catch { }

    try {
        const cacheDir = await igHandle.getDirectoryHandle('.fhir-package-cache');
        count++;

        try {
            await igHandle.getFileHandle('fhirpkg.lock.json');
            count++;
            const lockContent = await (await igHandle.getFileHandle('fhirpkg.lock.json')).getFile();
            lockFile = JSON.parse(await lockContent.text());
        } catch { }

        if (manifest?.dependencies && lockFile?.dependencies) {
            for (const packageId of Object.keys(manifest.dependencies)) {
                const version = lockFile.dependencies[packageId];
                if (!version) continue;
                const cacheFolderName = `${packageId}#${version}`;
                try {
                    const packageDir = await cacheDir.getDirectoryHandle(cacheFolderName);
                    const artifactsDir = await packageDir.getDirectoryHandle('package');
                    count += await countPackageArtifacts(artifactsDir);
                } catch { }
            }
        }
    } catch { }

    return count;
}

async function countPackageArtifacts(dirHandle) {
    let count = 0;
    for await (const entry of dirHandle.values()) {
        if (entry.kind === 'file') {
            if (shouldReadJsonFile(entry.name)) {
                count++;
            }
        } else if (entry.kind === 'directory') {
            if (shouldRecurseIntoDir(entry.name)) {
                count += await countPackageArtifacts(entry);
            }
        }
    }
    return count;
}

async function readPackageArtifactsWithProgress(
    dirHandle, prefix, batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igName, igIndex, igCount) {
    for await (const entry of dirHandle.values()) {
        const path = `${prefix}/${entry.name}`;
        if (entry.kind === 'file') {
            if (!shouldReadJsonFile(entry.name)) {
                continue;
            }
            const file = await entry.getFile();
            const base64 = await readFileAsBase64(file);
            await addToBatch({ path, base64 }, batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igName, igIndex, igCount);
        } else if (entry.kind === 'directory') {
            if (!shouldRecurseIntoDir(entry.name)) {
                continue;
            }
            await readPackageArtifactsWithProgress(
                entry, path, batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igName, igIndex, igCount);
        }
    }
}

async function readPackageArtifactsLegacy(dirHandle, prefix, files) {
    for await (const entry of dirHandle.values()) {
        const path = `${prefix}/${entry.name}`;
        if (entry.kind === 'file') {
            if (!shouldReadJsonFile(entry.name)) {
                continue;
            }
            const file = await entry.getFile();
            const base64 = await readFileAsBase64(file);
            files.push({ path, base64 });
        } else if (entry.kind === 'directory') {
            if (!shouldRecurseIntoDir(entry.name)) {
                continue;
            }
            await readPackageArtifactsLegacy(entry, path, files);
        }
    }
}

async function addToBatch(fileDto, batch, dotNetRef, getFilesRead, setFilesRead, totalFiles, igName, igIndex, igCount) {
    batch.push(fileDto);
    const current = getFilesRead() + 1;
    setFilesRead(current);

    if (batch.length >= BATCH_SIZE) {
        await flushBatch(batch, dotNetRef);
        await reportProgress(
            dotNetRef,
            'ReadingFiles',
            'Reading package files',
            current,
            totalFiles,
            igName,
            READING_STEP,
            igIndex,
            igCount,
            computeOverallReading(current, totalFiles));
        await yieldToBrowser();
    }
}

async function flushBatch(batch, dotNetRef) {
    if (batch.length === 0) {
        return;
    }
    await dotNetRef.invokeMethodAsync('OnFilesBatch', batch.splice(0, batch.length));
    await yieldToBrowser();
}

function shouldReadJsonFile(name) {
    if (!name.endsWith('.json')) {
        return false;
    }
    return name !== '.index.json' && name !== '.firely.index.json';
}

function shouldRecurseIntoDir(name) {
    const dirName = name.toLowerCase();
    return dirName !== 'other' && dirName !== 'openapi' && dirName !== 'xml';
}

function yieldToBrowser() {
    return new Promise(resolve => setTimeout(resolve, 0));
}

function readFileAsBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => {
            const dataUrl = reader.result;
            const base64 = dataUrl.split(',')[1];
            resolve(base64);
        };
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(file);
    });
}
