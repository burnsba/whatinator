# CLAUDE.md -- Whatinator.Core

The library. Essentially all logic lives here; `Whatinator.Cli` is a thin
argument-parsing and console-presentation shell over it. Read the root
[`CLAUDE.md`](../../CLAUDE.md) first -- especially **§ Gotchas**, which this
project's doc comments reference by name throughout.

Target: `net10.0`, nullable enabled, `InternalsVisibleTo("Whatinator.Core.Tests")`.
Only package reference: `System.IO.Hashing` (CRC32, for the test/copy compare).
Project reference: `Whatinator.LibDiscId`.

## Design rules this project follows

- **One thing per class.** `WhatinatorRipRunner` rips and does not package.
  `FlacPackager` packages and does not rip. `PipelineRunner` composes them and
  knows about exactly one disc per call. Multi-disc looping and every disc-swap
  prompt is the caller's job. Preserve this when adding anything.
- **No console I/O in Core.** Progress is written to `Stream standardOutput` /
  `Stream standardError` parameters the caller supplies. `MetadataService`
  deliberately returns an `Ambiguous` result instead of prompting -- the picker
  is the CLI's.
- **No hardware or network in the unit tests.** Every subprocess and HTTP
  dependency sits behind an interface (`ICdParanoiaTrackReader`, `IFlacEncoder`,
  `IAccurateRipClient`, `IMusicBrainzClient`, `IDiscogsClient`,
  `ICoverArtClient`, `ICdrdaoTocReader`). Orchestration classes have an
  `internal` constructor taking fakes; process-building logic is factored into
  `internal static BuildStartInfo(...)` so the argument vector can be asserted
  without spawning anything. Follow that pattern rather than inventing a new one.
- **Options in, result out.** Each operation takes a `record` options object
  (`WhatinatorRipOptions`, `FlacPackageOptions`, `Mp3PackageOptions`,
  `PipelineDiscOptions`, `CdParanoiaTrackOptions`, `EacLogOptions`) and returns a
  `record` result. Adding a parameter means adding a trailing optional property
  to the record, which keeps existing call sites compiling.

## Folder map

| Folder | What lives there |
| --- | --- |
| `AccurateRip/` | Checksum + disc-ID math and the database client. `AccurateRipChecksum` (v1/v2), `AccurateRipDiscId`, `CddbDiscId`, `AccurateRipClient` (binary dBAR parse), `WavFile`/`WavFormat` (header parse, `ReadDataChunk`). |
| `Checksums/` | `checksum_sha256.txt` generate/compare (`ChecksumFile`). Format-agnostic -- it hashes whatever's in the folder. |
| `CoverArt/` | Cover Art Archive fetch (`CoverArtClient`) + ImageMagick shrink/convert (`CoverArtProcessor`, best-effort). |
| `Discogs/` | Discogs search by barcode. Enrichment only; never blocking. |
| `Drive/` | Physical drive concerns: `OpticalDriveLocator` (sysfs enumeration), `OffsetFinder` (AccurateRip-confirmed read-offset calibration), `CacheDefeatAnalyzer` (`cd-paranoia -A`). |
| `Flac/` | `FlacEncoder` (`flac --verify`, tagged) and `FlacPackager` (release folder assembly). |
| `Metadata/` | The editorial model (`ReleaseInfo`/`MediumInfo`/`TrackInfo`), its JSON file (`ReleaseInfoFile`), lookup orchestration (`MetadataService`), and the corrections path (`MetadataUpdater`). |
| `Mp3/` | `LameEncoder` (V0) , `Mp3Packager`, `Mp3LogFile`. |
| `MusicBrainz/` | `MusicBrainzClient` plus the `Mb*` DTOs (all `internal` -- they never leak past the client). |
| `Naming/` | Every filesystem name decision. `FileNameSanitizer`, `ReleaseFolderNaming`, `FlacFolderNaming`, `Mp3FolderNaming`, `TrackFileNaming`. |
| `Rip/` | The extraction path. `CdParanoiaTrackReader`, its progress parsing trio (`CdParanoiaProgressLine`/`CdParanoiaProgressReporter`/`CdParanoiaLiveOutputFilter`), `WhatinatorRipRunner`, `PipelineRunner`, `WhatinatorEacLog`, `TrackFileMatcher`, `ProcessOutputRelay`. |
| `Toc/` | The physical model. `CdrdaoTocReader` (runs `cdrdao read-toc`), `TocFileParser` (parses the `.toc` text), `DiscToc`/`DiscTocTrack`. |
| root | `WhatinatorConfig` + `ConfigLoader`, `IdTextFile`, `M3uPlaylist`, `SystemInfo`, `WhatinatorLogHeader`, `WhatinatorVersion`, `WhatinatorUserAgent`. |

## Key types, in dependency order

**Physical:** `DiscToc` <- `TocFileParser` <- `CdrdaoTocReader`.
Frames, `IsAudio`, `PregapFrames`, `Isrc`, `CatalogNumber`. No titles.

**Editorial:** `ReleaseInfo` <- `MusicBrainzClient` (+ `DiscogsClient`) via
`MetadataService`; persisted by `ReleaseInfoFile` as `releaseinfo.json`.
Titles, artists, dates, `Media[]`. No frames.

**Extraction:** `CdParanoiaTrackReader.ReadTrackAsync` -> `CdParanoiaTrackResult`
(`Matched`, `WavPath`, `Crc32`, `Peak`, `Quality`, `Attempts`, `ElapsedTime`).
`WhatinatorRipRunner.RipAsync` -> `WhatinatorRipResult` (per-track
`WhatinatorTrackRipResult` + the whole-disc `AccurateRipMatchResult`).

**Packaging:** `FlacPackager.PackageAsync(FlacPackageOptions)` ->
`FlacPackageResult`; `Mp3Packager.PackageAsync(Mp3PackageOptions)` ->
`Mp3PackageResult`. `PipelineRunner.RunDiscAsync(PipelineDiscOptions)` ->
`PipelineDiscResult` composes TOC + rip + both packagers for one disc.

**Reporting:** `WhatinatorEacLog.Format(EacLogOptions)` renders the EAC-shaped
rip log; `IdTextFile.Format` renders `id.txt`; `M3uPlaylist` the playlist;
`ChecksumFile` the manifest; `Mp3LogFile` the MP3-side log.

## Invariants worth not breaking

- **Every operation is re-runnable.** The packagers rescan the container folder
  and rewrite `releaseinfo.json`, `id.txt`, `checksum_sha256.txt`, `.m3u`, and
  cover art from whatever is on disk. Never compute a container-level artifact
  from only the disc currently in hand.
- **Track pairing is positional.** Audio track *N* of the `DiscToc` pairs with
  track *N* of `ReleaseInfo.Media[disc-1].Tracks`. `WhatinatorRipRunner` throws
  `InvalidOperationException` if that fails -- a metadata/disc mismatch is a real
  error, not something to paper over.
- **Degraded is not failed.** A track that exhausts `MaxRetries`, a missing peak
  (no `sox`), a missing cover art, a Discogs miss, an AccurateRip miss -- all
  continue with a warning. The only hard failures are: no `.flac` files to
  package, `flac`/`lame` returning non-zero, a disc-number out of range, and
  the metadata/TOC mismatch above.
- **`ReleaseFolderNaming.ResolveDiscNumber` is the single gate** for disc-number
  validation. Don't re-implement the "required when multi-disc" rule elsewhere.
- **`FileNameSanitizer` is the only path to a filesystem name.** Everything in
  `Naming/` ends with a call to it.
- **`WavFile.ReadDataChunk` is what feeds `AccurateRipChecksum`** -- raw PCM, no
  header. Passing a whole WAV file in would silently corrupt every checksum.
- **`CdParanoiaTrackReader.IsExpectedSize` is the size guard** for a read
  (`(EndFrame - StartFrame + 1) * 2352 + 44`). `OffsetFinder` reuses it; that's
  why it and `RunCdParanoiaAsync` are `internal` rather than `private`.
- **Physical per-disc facts (ISRC, UPC) are threaded through options records,
  not stored on `ReleaseInfo`.** `DiscToc.CatalogNumber`/`DiscTocTrack.Isrc`
  flow straight into `FlacEncodeOptions.Isrc`, `EacLogOptions.Toc`, and
  `FlacPackageOptions.DiscCatalogNumber` on each call -- see root `CLAUDE.md`
  § Gotchas.

## Gotchas specific to this project

Everything in the root `CLAUDE.md` § Gotchas applies. Additionally:

- **`CdParanoiaProgressReporter` spans multiple process invocations.** One
  reporter is created per *track* and carries its state across the test read,
  the copy read, and every retry. The caller owns `BeginRead`/`Complete`;
  `RunCdParanoiaAsync` deliberately does not manage that lifecycle.
- **`cd-paranoia` progress arrives on stderr**, interleaved with real errors.
  `CdParanoiaLiveOutputFilter` separates them. Don't assume stderr means failure.
- **The `Mb*` DTOs are `internal` on purpose.** MusicBrainz's JSON shape must not
  leak into `ReleaseInfo`; the client maps across the boundary.
- **`MusicBrainzClient` has a retry with an injectable delay** (`internal`
  constructor taking `Func<TimeSpan, Task>`) so retry tests don't sleep. Rate
  limiting is real -- MusicBrainz requires a descriptive `User-Agent`.
- **`AccurateRipClient` parses a binary format**, not JSON. Its fixture test uses
  a real captured database response; keep that test passing.
- **`SystemInfo` is MP3-log-oriented** and captures fresh at encode time. The
  FLAC/rip log's environment comes from `RipEnvironmentInfo` gathered at rip
  time. They are intentionally separate.
- **`TocFileParser` skips `CD_TEXT` blocks** by brace-depth counting. If you add
  a CD-Text consumer, that's where to start.

## Testing

`Whatinator.Core.Tests` mirrors the folder structure. Patterns in use:

- `StubHttpMessageHandler` for every HTTP client test.
- `Fake*` implementations (`FakeMusicBrainzClient`, `FakeCoverArtClient`, and
  the fakes declared inline in `WhatinatorRipRunnerTests`) for orchestration.
- `DiscTocTestData` for shared TOC fixtures.
- `AccurateRip/Fixtures/*.bin` -- a real captured AccurateRip response, copied to
  output via the csproj.
- Assertions against `BuildStartInfo(...)`'s `ArgumentList` instead of running
  the tool.

Some tests do invoke real `ffprobe`, `sox`, and `magick` to verify encoder
output. They **fail rather than skip** when those tools are absent -- that's a
known rough edge, not an intended design.
