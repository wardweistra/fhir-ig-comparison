# FHIR IG Comparison

Compare multiple FHIR Implementation Guide (IG) packages locally in the browser. Built with **Blazor WebAssembly**, **Firely .NET SDK**, and hosted on **Firebase Hosting**. Package files are read via the [File System Access API](https://developer.mozilla.org/en-US/docs/Web/API/File_System_Access_API) and are not uploaded to a server.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or adjust target framework in `.csproj`)
- [Node.js](https://nodejs.org/) (for Firebase CLI via `npx`)
- **Chrome or Edge** (File System Access API; Safari/Firefox are not supported)

## Comparison folder layout

Select a **root folder** whose **immediate subfolders** are IGs. Each IG subfolder must be set up with [Firely Terminal](https://fire.ly/products/firely-terminal/):


```bash
cd "US Core"
fhir install hl7.fhir.us.core
fhir cache use-local
fhir restore
```

```text
test-case-1/
  US Core/
    package.json              # lists primary dependency (e.g. hl7.fhir.us.core)
    fhirpkg.lock.json         # resolved versions
    .fhir-package-cache/
      hl7.fhir.us.core#9.0.0-ballot/package/*.json
  NL Core/
    ...
  UK Core/
    ...
```

Each IG subfolder must have `package.json`, `fhirpkg.lock.json`, and `.fhir-package-cache/` with the **primary** package from `package.json` dependencies. Only that primary package is loaded for comparison (~400 JSON files per IG, not the full cache).

See [`test-cases/test-case-1/`](test-cases/test-case-1/) for an example.

## Local development

```bash
dotnet restore
dotnet run --project FhirIgComparison.Web
```

Open the URL shown in the console (typically `https://localhost:7xxx`).

## Build and deploy to Firebase

This app is a static Blazor WebAssembly site deployed to [Firebase Hosting](https://firebase.google.com/docs/hosting). The project is configured in `.firebaserc` (`ig-compare`) and `firebase.json` (serves `FhirIgComparison.Web/bin/Release/net10.0/publish/wwwroot`).

### First-time setup

1. Create or open the Firebase project in the [Firebase Console](https://console.firebase.google.com/) (project ID: `ig-compare`).
2. Log in to the Firebase CLI:

   ```bash
   npx -y firebase-tools@latest login
   ```

   If your credentials expire later, run `npx -y firebase-tools@latest login --reauth`. On a headless machine, add `--no-localhost`.

3. Confirm the active project (should print `ig-compare`):

   ```bash
   npx -y firebase-tools@latest use
   ```

   To deploy to a different Firebase project, run `npx -y firebase-tools@latest use --add YOUR_PROJECT_ID` and update `.firebaserc`.

### Deploy

From the repository root:

```bash
dotnet publish FhirIgComparison.Web -c Release
npx -y firebase-tools@latest deploy --only hosting
```

Live URLs after a successful deploy:

- https://ig-compare.web.app
- https://ig-compare.firebaseapp.com

### Preview before production

To publish to a temporary preview channel instead of production:

```bash
dotnet publish FhirIgComparison.Web -c Release
npx -y firebase-tools@latest hosting:channel:deploy preview
```

## How it works

1. **Pick folder** — JavaScript reads each IG subfolder: `fhirpkg.lock.json` + primary package under `.fhir-package-cache/{id}#{version}/package/`.
2. **Match resources** — Resources are indexed by canonical URL and grouped across IGs (full / partial / unique). National IGs (US / NL / UK) usually have different canonical URLs, so most groups will be **Unique** until richer matching is added.
3. **Compare StructureDefinitions** — For matched profiles in 2+ IGs, snapshot elements are aligned by `ElementId` with per-field mismatch highlighting (short, cardinality, etc.).

## Browser compatibility

| Feature | Chrome / Edge | Firefox | Safari |
|---------|---------------|---------|--------|
| File System Access API | Yes | No | No |
| Blazor WASM + Firely | Yes | Yes* | Yes* |

\*Folder picking requires Chromium; other browsers can run the app but cannot select local folders.

## Project structure

- `FhirIgComparison.Core` — Package loading, resource matching, StructureDefinition comparison
- `FhirIgComparison.Web` — Blazor WASM UI and `wwwroot/js/folderAccess.js`
- `firebase.json` — Hosting config pointing at publish output

## Related work

Element-level comparison logic is adapted from the [compare-fhir-ig](https://github.com/) sample (manual JSON paste); this app adds multi-IG folder loading and canonical URL matching.
