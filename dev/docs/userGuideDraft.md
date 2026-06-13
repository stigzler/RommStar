# User Guide Draft

## Key Points that User MUST know

- If using Launchbox scrape in Romm, **MUST** use "Local" (as the local LB Game db uses the db3 ids for launchboxdatabaseId). If uses "Cloud" - will get very mismatched results!
- User MUST be careful about romm auto-import given
- It is better to have the romm server in a 'canon' state (ie. all games are 'sealed' and no deleting re-adding will take place). Game CAN be added to the server platform, but not existing rejigged.

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

