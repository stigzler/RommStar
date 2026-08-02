# User Guide Draft

## Key Points that User MUST know

### For RommStar

- If using Launchbox scrape in Romm, **MUST** use "Local" (as the local LB Game db uses the db3 ids for launchboxdatabaseId). If uses "Cloud" - will get very mismatched results!
- User MUST be careful about romm auto-import to avoid contension over metadata scrapes.
- It is better to have the romm server in a 'canon' state (ie. all games are 'sealed' and no deleting re-adding will take place). Game CAN be added to the server platform, but not existing rejigged.
- Your rom sets matter!! RommStar discerns things like multi-disk games from the filenames. Make sure you use standards (list which rommstar recognises). You can do this by hand later via edit metadata in launchbox.
- Unlikely to work with 'portable' launchbox installations as a lot of file paths get resolved to absolute paths rather than relative + changing this would be a huge refactoring job.

### For RomM Server maintainers

- Use Screenscraper. This gets lots more media items (clear logo etc) that can populate your launchbox clients more fully.
-

App could check settings and warn user at some point: relevant settings:

```xml
    <AmazonAutoImport>InstalledOnly</AmazonAutoImport>
    <EaAutoImport>InstalledOnly</EaAutoImport>
    <EpicGamesAutoImport>InstalledOnly</EpicGamesAutoImport>
    <GogAutoImport>InstalledOnly</GogAutoImport>
    <SteamAutoImport>InstalledOnly</SteamAutoImport>
    <UbisoftAutoImport>InstalledOnly</UbisoftAutoImport>
    <XboxAutoImport>InstalledOnly</XboxAutoImport>
    <EnableRomAutoImports>true</EnableRomAutoImports>
```

Could also check badges:100:

```xml
<ShowBadges>true</ShowBadges>
<EnabledBadges>Installed|Not Installed|Achievements|rommstarSynced</EnabledBadges>
```

## Setup to dos

- Enable Rombox badges in Badges>Plugins. GameAttributes>INstalled/Not Installed

## Romm to Launchbox Progress map

| Progress State | NowPlaying | Backlogged | Status |
| :--- | :---: | :---: | :--- |
| **Not Started / Unplayed** | ❌ | ❌ | `"Incomplete"` or `null` |
| **Not Started / Want to Play** | ❌ | ✔️ | `"Incomplete"` or `null` |
| **Not Started / Won't Play** | ❌ | ❌ | `"Never Playing"` |
| **Active / In Progress** | ✔️ | ❌ | `"incomplee"` |
| **Active / Continuous** | ✔️ | ❌ | `null` |
| **Active / Paused** | ✔️ | ✔️ | `null` |
| **Done / Beaten** | ❌ | ❌ | `"Finished"` |
| **Done / Completed** | ❌ | ❌ | `"Completed 100%"` |
| **Done / Mastered** | - | - | *[Not Used given cannot map to romm]* |
| **Done / Dropped** | ❌ | ❌ | `"retired"` |