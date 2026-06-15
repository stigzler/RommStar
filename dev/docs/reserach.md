# Research

## iNKORE + UI

### Window rendering

Make sure you put this at the top of each page:100:

```xml
<page
      TextOptions.TextFormattingMode="Ideal"
      TextOptions.TextRenderingMode="ClearType"
      UseLayoutRounding="True"
      SnapsToDevicePixels="True"
/>
```

### Images

Use this in the xaml:

```xml
<Image Height="32" Width="32" Margin="0,0,10,0"
     Source="{Binding SelectedPlatform.IconPath, Mode=TwoWay,
              Converter={StaticResource StringToImageSourceConverter},              Converter={StaticResource StringToImageSourceConverter},              Converter={StaticResource StringToImageSourceConverter},
              ConverterParameter=32}"              ConverterParameter=32}"              ConverterParameter=32}"
     RenderOptions.BitmapScalingMode="NearestNeighbor"
     VerticalAlignment="Center" /> 
```

**Converter parameter**: is the rough target width the image will be. This helps with memory management.

**BitmapScalingMode:**

NearestNeighbor: For For Pixel Art / Small Icons. This stops WPF from blending pixels together and forces it to maintain perfectly sharp, crisp borders.

HighQuality: For larger items such as photos, boxart etc. Forces Fant/Bicubic resampling)

### Text and styles

Accent button:

```xml
<Button Style="{DynamicResource {x:Static ui:ThemeKeys.AccentButtonStyleKey}}"
     ToolTip="Syncs this Launchbox Platform with the Target Romm Server"
     Padding="16,8" Margin="0,0,10,0" FontWeight="DemiBold"
        Visibility="{Binding SelectedPlatform, Converter={StaticResource NullToVisibilityConverter}}">        Visibility="{Binding SelectedPlatform, Converter={StaticResource NullToVisibilityConverter}}">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="Sync Platform" />        <TextBlock Text="Sync Platform" />
        <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.SwipeDown_20_Regular}" Margin="5,2,0,0" />        <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.SwipeDown_20_Regular}" Margin="5,2,0,0" />
    </StackPanel>
</Button>
```

To use custom styles on visual element example:100:

```xml
<Border Background="{DynamicResource {x:Static ui:ThemeKeys.SystemControlBackgroundAccentBrushKey}}" BorderBrush="#80000000" BorderThickness="1"
CornerRadius="14" Padding="10,4" Margin="0,0,8,8"/>
```

## Romm API

### Roms

Romm has no Game object, just Rom.

Has **sibling_roms** - different versions of the game. Eg. 1942 Commodore 64 ("music 1" and "music 2").

Way to discern multi disc games from those with sibling roms:

has_simple_single_file - should always be opposite of has_multiple_files?
has_multiple_files
has_nested_single_file

Best approach to download all = iterate through all roms in that through files object?

If a game has multiple roms (eg. FF7) these are stored in and array of **files** which contains:

file.id (in individual id for the file)
file.romm_id (matched the parent rom)
file_path (could be the same as the other files - for eg for ff9 they share "roms/psx/Final Fantasy IX")

## Launchbox Plugin API

### Design

#### XML file and fields

Game holds master game. For multi disc games, all disks are held as "additionalApplicaitons" Looks like these are derived from the rom name?

|Element|Description|
|-|-|
|ID|Unique local identifier for the game|
|DatabaseID|The ID in the local db3 file|

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

## Romset standards

### No-Intro and Redump

Use same standard.

To get 'Disc' for lb:

For both, media types restricted to:

**Disc|Part|Side|Tape|Disk|Card**

These 6 cover 99.5% of all filenames

### TOSEC

Title (Year)(Publisher)[Extras][Flags]

Instead of (Disk 1), TOSEC uses a (Media X of Y) pattern for explicit multi-part counts, or standard strings for media orientations.

According to official TNC documentation, the absolute standard uses these exact patterns:

Counting Layout: (Disk 1 of 4), (Disc 3 of 6), (Tape 2 of 2)

Double-Digit Padding: If a game has 10 or more disks, TOSEC strictly forces double-digit padding: (Disk 06 of 13).

Compound Spacing/Side Layouts: TOSEC allows combining media counts and physical sides into a single block: (Tape 2 of 2 Side B) or simple (Side A).

Range Sets: Multiple disks stored inside one virtual archive image file are noted with hyphens: (Part 1-2 of 3).

```text
Sid Meier's Civilization v1.0 (1991)(MicroProse)(M4)(Disk 1 of 4).adf
International Karate + (1988)(System 3)(Side A).idx
Grand Prix (1991)(MicroProse)(Track Disk).adf
```

Tosec restricts media to:

**Disc|Disk|Part|Tape|Side|File**

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

