# whatinator

A CLI orchestrator for ripping CDs: reads a disc's TOC via `cdrdao`, reads
and verifies each track via `cd-paranoia`, encodes directly to FLAC (via
`flac --verify`) and MP3 V0 (via `lame`) alongside it, verifies against the
AccurateRip database, writes an EAC-style rip log, and looks up release
metadata from MusicBrainz and Discogs. 

## Status

Core functionality is complete, code review findings are being addressed.

## Required software

whatinator is an orchestrator -- it drives external tools rather than decoding
audio itself. These must be on `PATH` (or installed as a shared library) at
runtime.

**Required** -- a rip cannot proceed without these:

| Tool | Used for | Debian/Ubuntu package |
| --- | --- | --- |
| `cdrdao` | `read-toc` -- the frame-accurate TOC, pregaps, ISRC, disc catalog number | `cdrdao` |
| `cd-paranoia` | the actual audio extraction (test/copy read-and-verify), and drive cache analysis | `libcdio-utils` |
| `flac` | `flac --verify` encoding and FLAC tagging | `flac` |
| `libdiscid` | the MusicBrainz disc ID, via this repo's own P/Invoke wrapper. Needs the runtime library `libdiscid.so.0` only -- no `-dev` package required | `libdiscid0` |

**Optional** -- each is used for one feature and degrades gracefully if missing:

| Tool | Used for | If absent | Debian/Ubuntu package |
| --- | --- | --- | --- |
| `lame` | V0 MP3 encoding (`mp3`, and `pipeline` unless `--no-mp3`) | MP3 output is unavailable | `lame` |
| `sox` | per-track peak level (`stats`) for the rip log | the log's Peak column shows `-` | `sox` |
| ImageMagick (`magick`) | shrinking/converting downloaded cover art to fit 1920x1080 JPEG | the original image is saved unchanged | `imagemagick` |
| `uname` | the OS line in the logs | logged as `unknown` | (coreutils, already present) |

whatinator also reads `PRETTY_NAME` from `/etc/os-release` and enumerates drives
from `/sys/class/block/sr*` -- both Linux-specific, and both non-fatal if absent.

Installing everything at once:

```sh
sudo apt install cdrdao libcdio-utils flac libdiscid0 lame sox imagemagick ffmpeg
```

(`ffmpeg` is there for the test suite, not for ripping -- see below.)

**Network services used** all best-effort except where noted:
- MusicBrainz (disc lookup)
- Discogs (enrichment by barcode)
- Cover Art Archive (cover art)
- AccurateRip database (rip verification; **required** for `offset-find`).

## Building

Requires the .NET 10 SDK.

```sh
dotnet build
dotnet test
```

Running the tests additionally needs `ffprobe` (package `ffmpeg`), `sox`, and
ImageMagick on `PATH` -- a handful of encoder tests shell out to them to inspect
real output, and those tests **fail rather than skip** when the tool is missing.

## Config file

Optional. Read from `$XDG_CONFIG_HOME/whatinator/config.json`, falling
back to `~/.config/whatinator/config.json`. If it doesn't exist, built-in
defaults are used -- you don't need to create one.

```json
{
  "device": "/dev/sr1",
  "makeMp3": true,
  "userAgent": "whatinator/1.0.11 ( you@example.com )",
  "readOffsets": { "ASUS|DRW-24F1ST   b|1.00": 6 },
  "cacheDefeats": { "ASUS|DRW-24F1ST   b|1.00": "CanDefeat" },
  "overreads": { "ASUS|DRW-24F1ST   b|1.00": true }
}
```

| Key | Default | Meaning |
| --- | --- | --- |
| `device` | `/dev/sr1` | Optical drive used when `--device` isn't given. |
| `makeMp3` | `true` | Whether `pipeline` creates MP3s by default (overridden by that command's own `--no-mp3`). |
| `userAgent` | `whatinator/{version} ( bethany.whatinator@burnsba.net )` | HTTP `User-Agent` sent with every MusicBrainz/Discogs/Cover Art Archive/AccurateRip request. Computed fresh from the running version unless set here; set it to substitute your own contact address. |
| `readOffsets` | none | Per-drive sample read offset, keyed by `"{vendor}\|{model}\|{release}"` (see `whatinator list-device` for those three values). Populated automatically by `whatinator offset-find`; can still be edited by hand if you already know a drive's offset. |
| `cacheDefeats` | none | Per-drive audio-cache-defeat result (`"CanDefeat"`/`"CannotDefeat"`/`"Unknown"`), same key shape as `readOffsets` -- feeds the rip log's "Defeat audio cache" field. Not run automatically (a full analysis takes real drive time); populate by hand after inspecting a drive with `cd-paranoia -A`. |
| `overreads` | none | Per-drive `--force-overread` support (`true`/`false`), same key shape as `readOffsets` -- a drive with a `true` entry gets `--force-overread` on every `rip`/`pipeline` run without needing `--overread` each time. Not every drive supports overreading (some error, some return silence), so populate by hand only after manually verifying a drive's behavior; `rip`/`pipeline --overread` forces it on for a single run regardless of this map. |

## Commands

```sh
dotnet run --project src/Whatinator.Cli -- --help
dotnet run --project src/Whatinator.Cli -- list-device
dotnet run --project src/Whatinator.Cli -- offset-find --device /dev/sr1
dotnet run --project src/Whatinator.Cli -- disc-info --device /dev/sr1 --ask
dotnet run --project src/Whatinator.Cli -- toc --device /dev/sr1
dotnet run --project src/Whatinator.Cli -- make-releaseinfo --device /dev/sr1
dotnet run --project src/Whatinator.Cli -- id-txt --releaseinfo releaseinfo.json
dotnet run --project src/Whatinator.Cli -- update-metadata --releaseinfo corrected-releaseinfo.json --dest "out/Artist - Album [flac 2001]"
dotnet run --project src/Whatinator.Cli -- pipeline --releaseinfo releaseinfo.json --dest out/
dotnet run --project src/Whatinator.Cli -- rip --releaseinfo releaseinfo.json --dest work/ --keep-wav
dotnet run --project src/Whatinator.Cli -- flac --releaseinfo releaseinfo.json --source work/ --dest out/
dotnet run --project src/Whatinator.Cli -- mp3 --releaseinfo releaseinfo.json --source "out/Artist - Album [flac 2001]" --dest mp3out/
dotnet run --project src/Whatinator.Cli -- make-checksum --dest "out/Artist - Album [flac 2001]"
dotnet run --project src/Whatinator.Cli -- compare-checksum --dest "out/Artist - Album [flac 2001]"
```

### Setup

One-time drive facts -- enumerate drives and calibrate a drive's sample read offset.

| Command | Description |
| --- | --- |
| `list-device` | List available optical drives. |
| `offset-find [--device <path>]` | Auto-detect the drive's sample read offset against the disc currently inserted (must already have a real entry in the AccurateRip database -- the command says so plainly and exits 1 if it doesn't, or if the disc has fewer than 3 audio tracks). Tries a ranked list of candidate offsets (most commonly correct first, sourced from AccurateRip's own public drive-offset database) until one produces a full match, then saves it to the config file's `readOffsets` map under the current drive's key, overwriting any prior entry for that same drive. Never guesses from a partial match -- if nothing matches, it says so and suggests trying a different disc. |

### Catalog

Identify a disc and produce/maintain its `releaseinfo.json`/`id.txt` metadata.

| Command | Description |
| --- | --- |
| `disc-info [--device <path>] [--ask]` | Read a disc's TOC and MusicBrainz disc ID (default device: config, else `/dev/sr1`), then best-effort look up the disc on MusicBrainz and print its artist, release title, and per-track titles/durations for every disc of the matched release. Multiple MusicBrainz matches: without `--ask`, uses the first match automatically and lists the others; with `--ask`, prompts for a selection on stdin, same picker as `make-releaseinfo` -- if no selection is made (e.g. stdin closes before a choice is entered), exits 1, matching `make-releaseinfo`. A MusicBrainz miss or lookup failure isn't fatal -- the disc's TOC info (already printed) is still useful on its own. Diagnostic command from phase 002, extended per `docs/plan/backlog-closed/track_info.md`. See also `toc`, for the frame-accurate technical read `rip`/`pipeline` use internally. |
| `toc [--device <path>] [--full]` | Read a disc's frame-accurate TOC via `cdrdao read-toc` and print its track table (start/length as time and frames, pregap, ISRC). Fast by default (`--fast-toc`, track start/length only, near-instant); `--full` additionally scans for per-track pregaps (much slower -- cdrdao has to scan audio content across every track boundary). Diagnostic command from phase 013 -- the detail level `disc-info`'s libdiscid-based read deliberately skips; no MusicBrainz involvement here at all. |
| `make-releaseinfo [--device <path>] [--releaseinfo <path>] [--dest <path>]` | Look up the disc on MusicBrainz (or, with `--releaseinfo`, use that file's content instead of doing a fresh lookup), best-effort enrich with a matching Discogs release (by barcode), and write `{dest}/releaseinfo.json` (default dest `.`) either way -- the command's job is always producing that file; `--releaseinfo` only changes where its content comes from. Zero MusicBrainz matches: prints the disc's known TOC info and exits 1, no file written. Multiple matches (MusicBrainz or Discogs): prompts for a selection on stdin (Discogs prompt includes a skip option). |
| `update-metadata --releaseinfo <path> --dest <path>` | Apply a corrected `releaseinfo.json` (`--releaseinfo`) to an already-packaged FLAC/MP3 release folder (`--dest`). Backs up the folder's existing `releaseinfo.json` to `releaseinfo.bak` (overwritten each run -- point a later `update-metadata` at it to revert), then overwrites `releaseinfo.json`, regenerates `id.txt`, and recalculates `checksum_sha256.txt` for whichever audio format (`.flac`/`.mp3`) is present, plus any `.log` file. Prompts for `y`/`n` confirmation on stdin if the artist or title differs from what's currently there. Renames the folder if its computed name (`"{Artist} - {Title} [flac/mp3 v0 {Year}]"`) no longer matches -- covers both a year correction and an artist/title correction. Doesn't touch individual audio files, tags, or the `.m3u`. |
| `id-txt --releaseinfo <path> [--dest <path>]` | Generate `id.txt` from a saved `releaseinfo.json` (`--releaseinfo` required), written to `{dest}/id.txt` (default dest `.`). One file per release -- for a multi-disc release, put it in the folder that contains `cd1/`, `cd2/`, etc., not inside a disc subfolder. No network calls. |

### Convert

Rip a disc and turn it into FLAC/MP3 output.

| Command | Description |
| --- | --- |
| `pipeline [--releaseinfo <path>] [--device <path>] [--dest <path>] [--multi <start>-<end>] [--no-flac] [--no-mp3] [--keep-wav] [--fast-toc] [--overread]` | The full rip → FLAC-packaging → MP3 pipeline in one command. Resolves the release the same way `make-releaseinfo` does (or loads `--releaseinfo` directly) and always saves `{dest}/releaseinfo.json`, then rips/packages every disc in range (default: all of them; `--multi 1-3` limits the run), prompting to swap discs between each one on a multi-disc release. `--no-flac` skips FLAC packaging and keeps the raw rip output on disk unorganized instead of deleting it; `--no-mp3` skips MP3 encoding for this run (default comes from the config file's `makeMp3`); `--keep-wav` retains each track's accepted WAV alongside its `.flac`. A track that can't be read after retries doesn't abort the disc -- whatever was captured is still packaged, with a warning printed. The TOC read scans every track's pregap by default (costing roughly a second per track or more); `--fast-toc` skips that scan and reports only track 1's pregap, same as `toc`'s default (note the opposite polarity -- `pipeline`/`rip` scan by default, `toc` doesn't). `--overread` passes `--force-overread` to every track read (also forced on for a drive with a `true` entry in the config file's `overreads` map); not every drive supports it. |
| `rip --releaseinfo <path> [--device <path>] [--dest <path>] [--disc <N>] [--keep-wav] [--fast-toc] [--overread]` | Rip the disc in the drive: `cdrdao` reads the TOC, then for every audio track `cd-paranoia` does a test/copy read-and-verify, `flac --verify` encodes the accepted WAV (tagged), and finally an AccurateRip database lookup verifies the whole disc's checksums. Writes an EAC-style rip log (`{dest}/{Artist} - {Title}.log`) with drive/settings info, the full TOC, and per-track peak/speed/CRC/AccurateRip results. `--disc <N>` is required for releases with more than one disc. `--keep-wav` retains each track's accepted WAV alongside its `.flac` instead of deleting it after a successful encode. A track that can't be read after retries is skipped (warned about, not fatal) rather than aborting the disc. The TOC read scans every track's pregap by default (costing roughly a second per track or more); `--fast-toc` skips that scan and reports only track 1's pregap. `--overread` passes `--force-overread` to every track read (also forced on for a drive with a `true` entry in the config file's `overreads` map); not every drive supports it. |
| `flac --releaseinfo <path> --source <path> [--dest <path>] [--disc <N>]` | Package a rip's FLAC output (`--source`, from `rip`'s `--dest`) into `{Artist} - {Title} [flac {Year}]/` under `--dest` (default `.`). `--disc <N>` is required for releases with more than one disc (files go in `cd1/`, `cd2/`, etc.). Moves the FLAC files (and any retained `.wav` files, from `rip --keep-wav`) and the rip log written by `rip`/`pipeline`, writes this disc's `.cue` sheet (`CATALOG`/`ISRC`/pregap data from the disc's TOC where known), then writes/refreshes `id.txt`, `releaseinfo.json`, `checksum_sha256.txt` (covering the `.flac`, `.log`, and `.cue` files only -- see Verify below), `.m3u`, and cover art (best-effort, from the MusicBrainz Cover Art Archive) at the release level. Not written for `mp3`'s output -- a cue sheet exists to make a rip losslessly reconstructible to disc, which a lossy MP3 encode can't serve. Safe to run once per disc across separate sessions. |
| `mp3 --releaseinfo <path> --source <path> [--dest <path>] [--disc <N>]` | Encode a `flac`-packaged disc folder (`--source`) to V0 MP3 via `lame` into `{Artist} - {Title} [mp3 v0 {Year}]/` under `--dest` (default `.`). `--disc <N>` is required for releases with more than one disc. Fully tags each MP3 (title/artist/album/album artist/year/track/genre -- no embedded cover art, by user decision; cover art is still copied alongside the MP3s from the FLAC folder, never re-fetched) in one `lame` invocation per track, then writes/refreshes `id.txt`, `releaseinfo.json`, `checksum_sha256.txt` (covering the `.mp3` and `.log` files only), `.m3u`, and its own log (OS info, timestamps, lame version -- independent of the FLAC log) at the release level. No network calls. Safe to run once per disc across separate sessions. |

### Verify

Hash/check a folder's contents against a manifest -- independent of any particular release format.

| Command | Description |
| --- | --- |
| `make-checksum [--dest <path>]` | Recursively hash every file under a folder (except the manifest itself) and write `{dest}/checksum_sha256.txt` (default dest `.`). Not filtered by extension -- works on any folder, not just a FLAC/MP3 release folder. This is a distinct, deliberately format-agnostic tool: it hashes everything, unlike the `flac`/`mp3` commands' own manifest (audio + log only), so running it over a packaged release folder produces a broader manifest than packaging did. |
| `compare-checksum [--dest <path>]` | Read `{dest}/checksum_sha256.txt` and compare it against what's actually there: reports matched/mismatched/missing/malformed/extra counts (with details for anything not matched). A manifest entry whose path escapes the target folder (`..` traversal or an absolute path) is reported as "malformed" and neither read nor hashed. Exits `0` unless something listed in the manifest is mismatched, missing, or malformed; `1` otherwise -- files present on disk but not listed in the manifest ("extra") are reported but don't affect the exit code, since a packaged release folder always has some (`id.txt`, `releaseinfo.json`, cover art, `.m3u`) by design. |

### Info

| Command | Description |
| --- | --- |
| `help` / `--help` / `-h` | Show usage. |
| `--version` / `-v` | Show the current version. |
| `<command> --debug` | Print the full stack trace for an unhandled exception instead of a one-line message (same effect as setting the `WHATINATOR_DEBUG` environment variable). Must come after the command name, like any other flag. |

## Layout

```
CLAUDE.md                Orientation for an AI assistant: data flow, conventions, gotchas
src/                     C# projects (each has its own CLAUDE.md)
docs/plan/               Planning docs: roadmap, phase plans, decisions, demos
example/                 Reference EAC log / id.txt / m3u / cue samples
```

| Project | Role |
| --- | --- |
| `src/Whatinator.Core` | The library -- essentially all logic. |
| `src/Whatinator.LibDiscId` | Hand-written P/Invoke wrapper over native `libdiscid`. |
| `src/Whatinator.Cli` | Console front end, one file per command. |
| `src/Whatinator.Core.Tests`, `src/Whatinator.LibDiscId.Tests` | xunit test suites. |

## License

MIT -- see [`LICENSE`](LICENSE).
