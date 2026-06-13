# Implementation

## Romm > Launchbox Sync

### Scenarios

#### RommRom > LbRom matching

Additional booleans:

OverwriteMetadata, lbRom.ProtectMetadata, DeleteOldServerRoms

RommRom matches LbRom on:

|LaunchboxID|RommID|ServerId|Action|UX/Logic|
|-|-|-|-|-|
|F|F|T|Insert|Upsert New (this could be all roms from previous server sync)|
|F|F|F|Insert|Upsert New (given no match at all)|
|F|T|T|?Update|Upsert Existing on OverwriteMetadata && !lbRom.ProtectMetadat|
|F|T|F|?Delete + Insert|Delete existing on DeleteOldServerRoms. Upsert new|
|T|F|F|?Update|Upsert Existing on OverwriteMetadata && !lbRom.ProtectMetadata|
|T|F|T|?Update|Upsert Existing on OverwriteMetadata && !lbRom.ProtectMetadata (scenario = rom deleted from server and re-added)|
|T|T|T|?Update|Upsert Existing on OverwriteMetadata && !lbRom.ProtectMetadata|
|T|T|F|?Delete + Insert|Delete existing on DeleteOldServerRoms. Upsert new|
|T|null|null|?Update|Update existing on OverwriteMetadata && !lbRom.ProtectMetadata|
|F|null|null|Insert|Upsert New.|