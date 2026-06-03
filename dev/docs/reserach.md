# Research

## Plugin Api

### Retrieve a game

### Import System

Leverage existing LB systems:

 - IGame has a "Installed" property. When syncing Romm data - set this to false. This then change "Play" to "Import". Caveat: you have to have a file ref'd in botht he db entry and in the folder in order for Install to be active.

 System: have a placeholder file named after the game's rom (eg "Astro Chase (USA).romm"). Db entry also references this. All other game metadata as per romm

 Some working code that shows the process:

 ```cs
public void OnBeforeGameLaunching(IGame game, IAdditionalApplication app, IEmulator emulator)
{
    game.ApplicationPath = "C:\\Users\\stigz\\LaunchboxDEV\\LaunchBox\\Games\\Atari 5200\\Astro Chase (USA).zip";
    game.Installed = true;
    PluginHelper.DataManager.Save();

    // this mimics the download from romm:
    File.Copy("C:\\temp\\project tests\\rommster\\Astro Chase (USA).zip",
                "C:\\Users\\stigz\\LaunchboxDEV\\LaunchBox\\Games\\Atari 5200\\Astro Chase (USA).zip", true);
}

public void OnGameExited()
{
    File.Delete("C:\\Users\\stigz\\LaunchboxDEV\\LaunchBox\\Games\\Atari 5200\\Astro Chase (USA).romm");
}
```

The order of these is important. Above method derived by trial and error (think the folder watch service can interfere producing exceptions in any other order)

## Architecture

GPT-5:

Short recommendation: model services by responsibility (what they do), noteach LaunchBox event. Keep a thin plugin adapter that turns LaunchBox callbacks into calls/events and let small domain services handle the work.

Minimal set of services (overview)

- `PluginAdapter` (thin)
  - Implements LaunchBox interfaces (`PluginInterface` → `PluginHost`).
  - Normalizes inputs and calls domain services or publishes simple in-process events.
- `RommClientService`
  - Talks to your self-hosted romm API (auth, pagination, platform lists, game metadata, media URLs, rom file manifests).
- `SyncService`
  - “Sync platform” logic: given a platform, fetch romm metadata + media list, compare with local LaunchBox data, produce add/update actions.
- `GameRepository` (or `IGameStore`)
  - Keeps mappings and persistent state: romm IDs ↔ LaunchBox game IDs, download/install status, file locations, badges.
  - Backed by a small DB (SQLite) or a JSON file for a hobby project.
- `DownloadService`
  - Queue manager for downloads; handles multi-file games, retries, progress reporting, concurrent downloads limit.
  - Writes files to a configurable storage location (local folder abstraction).
- `InstallerService`
  - Moves/organizes files, integrates them into LaunchBox (adds/updates entries, sets application paths), and marks games installed in `GameRepository`.
- `MediaService`
  - Downloads art/videos and attaches them to LaunchBox entries or stores them in romm cache.
- `BadgeService`
  - Computes badge/status for each game: `DownloadedFromRomm`, `Installed`, `AvailableFromRomm`.
  - Reads from `GameRepository` and runtime download/installer state.
- `SettingsService`
  - Stores user preferences (romm URL, credentials, download folder, concurrency limits, platform mappings).
- `LoggingService`
  - Central logging for diagnostics.
- `BackgroundWorker` or `TaskQueue`
  - Runs long-running tasks (syncs, downloads) off the plugin callback thread so LaunchBox stays responsive.
- `IFileStore` abstraction (optional)
  - Allows swapping local filesystem vs other storage later.

How they work together (simple flow)

1. User selects “Sync platform” from UI → `PluginAdapter` calls `SyncService`.
2. `SyncService` uses `RommClientService` to fetch romm data, compares to `GameRepository`, and returns a set of actions (add/update).
3. `SyncService` calls `MediaService`/`GameRepository` to apply changes; badges updated by `BadgeService`.
4. User clicks “Install” on a game → `PluginAdapter` enqueues an install with `DownloadService`.
5. `DownloadService` downloads all required files, then calls `InstallerService`.
6. `InstallerService` places files, updates LaunchBox via its API (or CLI), and updates `GameRepository`. `BadgeService` reflects new state.

Practical choices for a hobby project

- Start simple: SQLite (via `System.Data.SQLite` or `Microsoft.Data.Sqlite`) or a single JSON file for `GameRepository`.
- Use a simple download queue with a limited degree of parallelism (e.g., `Task.Run` + `SemaphoreSlim`).
- Keep services as singletons wired in DI (`PluginHost.ConfigureServices`).
- Keep the plugin adapter tiny and synchronous: enqueue work to the background queue and return quickly.
- Log liberally while developing to see event flow.

Concurrency / correlation

- Use persistent IDs (romm ID, LaunchBox ID) as primary keys. Use process id only for ephemeral lifecycle correlation.
- Protect `GameRepository` writes with a single writer or a simple lock to avoid race conditions.

Where to expand later

- Add a background sync scheduler.
- Add a download resume/cache layer.
- Add unit tests by mocking `RommClientService` and `IGameStore`.

If you want, I can scaffold a small set of interfaces and a minimal `GameRepository` (JSON or SQLite) and wire them in `PluginHost`. Which should I create first: `IGameStore` + simple implementation, or `DownloadService` skeleton?

