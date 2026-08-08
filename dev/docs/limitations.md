# Limitations

## Due to Romm

1. **Metadata Sync + SHA1 varification:** cannot perform SHA1 checks on sub-files of a Romm `/api/roms/{romId}` return, as SHA1s not populated in the Files for the rom and `/api/roms/{id}/files` endpoint returns a Internal Server error. Issue posted on discord.

2. SHA1 value held in the romm rom record seem inconsistent. Eg. a single file PSX game (eg. `wipeout.zip`) has a sha1 listed that equals that of its zip file. However, likewise for an atari 5200 game (eg. `ballblazer.zip`) the sha1 is that of the .a52 file inside, NOT that of the zip file. All other properties of the romm record are equitable - therefore no indication about whether the sha fo the zip or the internal file should be used.

