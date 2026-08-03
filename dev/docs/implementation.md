# Implementation

## Romm > Launchbox Sync

### Scenarios

#### RommRom > LbRom matching

Additional booleans:

OverwriteMetadata, lbRom.ProtectMetadata, DeleteOldServerRoms

RommRom matches LbRom on:

LaunchboxID = LB database ID (not the local id)

|LaunchboxID|RommID|ServerId|Multi-Disc|Action|UX/Logic|
|-|-|-|-|-|---|
|F|F|T|n/a|Insert|Upsert New (this could be all roms from previous server sync)|
|F|F|F|n/a|Insert|Upsert New (given no match at all)|
|F|T|T|n/a|?Update|Upsert Existing on OverwriteMetadata && !lbRom.ProtectMetadat|
|F|T|F|n/a|?Delete + Insert|Delete existing on DeleteOldServerRoms. Upsert new|
|T|F|F|n/a|?Update|Upsert Existing on OverwriteMetadata && !lbRom.ProtectMetadata|
|T|F|T|n/a|?Update|Upsert Existing on OverwriteMetadata && !lbRom.ProtectMetadata (scenario = rom deleted from server and re-added)|
|T|T|T|n/a|?Update|Upsert Existing on OverwriteMetadata && !lbRom.ProtectMetadata|
|T|T|F|n/a|?Delete + Insert|Delete existing on DeleteOldServerRoms. Upsert new|
|T|null|null|n/a|?Update|Update existing on OverwriteMetadata && !lbRom.ProtectMetadata|
|F|null|null|n/a|Insert|Upsert New.|