# CLAUDE.md -- whatinator (repo root)

Guidance for an AI assistant picking up work in this repository. Project-specific
notes live in each project's own `CLAUDE.md`:

- [`src/Whatinator.Core/CLAUDE.md`](src/Whatinator.Core/CLAUDE.md) -- the library where nearly all logic lives.
- [`src/Whatinator.LibDiscId/CLAUDE.md`](src/Whatinator.LibDiscId/CLAUDE.md) -- the hand-written P/Invoke wrapper over native `libdiscid`.

## What this is

whatinator is a **CLI orchestrator** for making an "exact" (EAC-style) audio copy
of a CD. It does not implement audio extraction itself -- it drives external
tools, parses their output, verifies the results, and assembles a standard
release folder with logs, checksums, playlists, tags, and metadata.

It is Linux-first and single-user. There is no GUI, no daemon, no server.

## Layout

```
src/Whatinator.Core/            The library. Essentially all logic. ~100 files.
src/Whatinator.Core.Tests/      xunit tests for Core.
src/Whatinator.LibDiscId/       P/Invoke wrapper over native libdiscid.
src/Whatinator.LibDiscId.Tests/ xunit tests for the wrapper.
src/Whatinator.Cli/             Console front end. Thin: one *Command.cs per verb.
                                Delegates to Whatinator.Core for everything
                                except argument parsing/dispatch and console
                                I/O, which is what Whatinator.Cli.Tests covers.
src/Whatinator.Cli.Tests/       xunit tests for the CLI's pure argument
                                parsing/dispatch logic (ParsedOptions,
                                CommandDispatcher's error paths) -- not the
                                individual *Command.cs files, which still need
                                a drive/network and stay untested.
example/                        Reference EAC log / id.txt / m3u / cue samples,
                                kept as format targets. Not consumed by code.
docs/plan/                      Roadmap, phase plans, decisions, demos, backlog.
                                (Referenced throughout the code comments; not
                                present in every checkout.)
init.md                         The original project brief. Still the best
                                statement of user intent for output formats.
```

`Directory.Build.props` is the single place for the target framework (`net10.0`),
`<Version>`, and the analyzer package references. **Do not put a version literal
anywhere else** -- read it via `Whatinator.Core.WhatinatorVersion.Current`.

## Build and test

```sh
dotnet build            # warnings matter: CS1591 + StyleCop are on for non-test projects
dotnet test
```

Some tests shell out to real tools (`ffprobe`, `sox`, `magick`) to verify encoder
output and will fail -- not skip -- if those aren't installed. See the README's
required-software section.

## External tools this project drives

Everything is invoked with `UseShellExecute = false` and `ArgumentList` (never a
concatenated argument string). Production call sites:

| Tool | Where | Role | Required? |
| --- | --- | --- | --- |
| `cdrdao` | `Toc/CdrdaoTocReader.cs` | `read-toc` -> frame-accurate TOC, pregaps, ISRC, catalog number | yes |
| `cd-paranoia` | `Rip/CdParanoiaTrackReader.cs`, `Drive/CacheDefeatAnalyzer.cs` | the actual audio extraction; `-A` for cache analysis | yes |
| `flac` | `Flac/FlacEncoder.cs` | `flac --verify` encode + tagging | yes |
| native `libdiscid` | `Whatinator.LibDiscId` | MusicBrainz disc ID | yes |
| `lame` | `Mp3/LameEncoder.cs` | V0 MP3 encode + tagging | only for MP3 output |
| `sox` | `Rip/CdParanoiaTrackReader.cs` | `stats` -> per-track peak level for the log | optional, degrades to no peak |
| `magick` | `CoverArt/CoverArtProcessor.cs` | shrink/convert downloaded cover art | optional, degrades to original image |
| `uname` | `SystemInfo.cs` | log header | optional, degrades to `"unknown"` |

Web services: MusicBrainz, Discogs, the Cover Art Archive, and the AccurateRip
database. All are best-effort except AccurateRip during `offset-find`.

## The data flow

The single most useful thing to hold in your head:

```
disc in drive
  |
  |-- libdiscid ----------> MusicBrainz disc ID ---> MusicBrainz release
  |                                                    |-- Discogs (by barcode)
  |                                                    v
  |                                            ReleaseInfo  (releaseinfo.json)
  |                                            metadata model: artist/title/
  |                                            date/media[]/tracks[]. NO frames.
  |
  '-- cdrdao read-toc ----> DiscToc / DiscTocTrack
                            frame model: StartFrame/EndFrame/IsAudio/
                            PregapFrames/Isrc + CatalogNumber. NO titles.

  DiscToc + ReleaseInfo
       |
       v
  WhatinatorRipRunner  (per audio track)
       |-- CdParanoiaTrackReader: "test" read + "copy" read -> compare CRC32
       |                          retry on mismatch; sox for peak; quality %
       |-- AccurateRipChecksum: v1/v2 from the accepted WAV's PCM
       '-- FlacEncoder: flac --verify, tagged
       |
       '-- once all tracks read: AccurateRipClient whole-disc lookup
       |
       v
  WhatinatorEacLog -> "{Artist} - {Title}.log"
       |
       v
  FlacPackager -> "{SortArtist} - {Title} [flac {Year}]/"
       moves .flac/.wav/.log in, writes this disc's .cue sheet from DiscToc,
       then REGENERATES releaseinfo.json, id.txt, checksum_sha256.txt, .m3u,
       cover art by rescanning the folder
       |
       v
  Mp3Packager -> "{SortArtist} - {Title} [mp3 v0 {Year}]/"
       lame from the packaged FLACs, same artifact set + its own log
```

`PipelineRunner` composes TOC-read -> rip -> FLAC package -> MP3 package for **one
disc**. Looping over discs of a multi-disc release and prompting for the physical
swap is the CLI's job (`PipelineCommand`), not the runner's.

## The two disc models -- don't confuse them

This trips people up constantly:

- `Toc.DiscToc` / `Toc.DiscTocTrack` -- **physical**, from `cdrdao`. Frames,
  audio-vs-data, pregaps, ISRC, catalog number. No titles.
- `Metadata.ReleaseInfo` / `MediumInfo` / `TrackInfo` -- **editorial**, from
  MusicBrainz/Discogs. Titles, artists, durations, disc positions. No frames.

They are joined by position: audio track *N* in the TOC pairs with track *N* in
the medium. `WhatinatorRipRunner` throws if that pairing fails.

## Conventions

- **Every** class, method, and property -- public, internal, *and* private --
  carries an XML doc comment. `GenerateDocumentationFile` is on so CS1591 fires,
  and StyleCop's SA16xx rules back it up. Test projects are exempted via
  `NoWarn` in `Directory.Build.props`. Match the existing density: the comments
  explain *why*, including rejected alternatives and upstream-bug workarounds.
  That prose is load-bearing institutional memory -- do not strip it.
- Doc comments reference `docs/plan/phase-0NN.md` and "phase NNN" throughout.
  Those are historical provenance markers. Leave them alone; don't renumber.
- `ConfigureAwait(false)` on every await in library code and in the CLI.
- Records for data, sealed classes for behavior, static classes for pure
  functions. Interfaces (`IFlacEncoder`, `ICdParanoiaTrackReader`,
  `IAccurateRipClient`, `IMusicBrainzClient`, ...) exist as **test seams** so
  orchestration can be exercised without a drive or the network.
- `internal` constructors and `internal static` helpers are used deliberately as
  test seams; `Whatinator.Core` has `InternalsVisibleTo("Whatinator.Core.Tests")`.
- Definition of Done for any change: **update the root `CLAUDE.md` and any
  touched project's `CLAUDE.md` / `README.md` before calling it complete.**
  Also update `HelpContent.cs` *and* the README command tables together -- they
  drift apart easily.

## Gotchas

The code comments point here repeatedly (`see root CLAUDE.md § Gotchas`). These
are the non-obvious facts that will bite you.

### Ported algorithms: don't "fix" the reference implementations

`AccurateRipChecksum`, `AccurateRipDiscId`, and `CddbDiscId` are pure-C# ports of
public, externally-specified algorithms (AccurateRip v1/v2 per the HydrogenAudio
thread; the freedb CDDB1 disc ID). Their output must match other tools
bit-for-bit or lookups silently miss.

Two specific traps found during porting, both deliberately preserved:

- The sources these were researched against carry **read-but-never-used state**.
  `CddbDiscId`'s reference had a dead-code path; `CdParanoiaTrackReader.ComputeQuality`
  omits extra accounting its reference computes but never consults (it only feeds
  a debug line and a commented-out adjustment). If you see a variable that looks
  like it "should" affect the result, check whether it actually does in the
  original before wiring it in.
- `AccurateRipDiscId` excludes data tracks from the running sum, **but the last
  track on the disc -- audio or data -- still determines the leadout offset.**
  That asymmetry is correct and is what the reference does. `CddbDiscId`, by
  contrast, counts data tracks like any other track.

Any change to these three files needs the existing fixture tests to still pass,
including `AccurateRipClientTests`' captured live database response.

### Frame arithmetic

- 75 frames per second; 1 frame == 1 CD sector; 588 stereo 16-bit sample pairs
  per frame; 2352 bytes of audio per sector; a WAV header is 44 bytes.
- `DiscTocTrack.EndFrame` is **inclusive** -- the frame immediately before the
  next track's `StartFrame`. Track length is `End - Start + 1`. This matches the
  convention the ports were cross-checked against; getting it wrong shifts every
  checksum by one sector.
- `DiscToc.LeadoutFrame` is `Tracks[^1].EndFrame + 1`.
- `CddbDiscId` adds a fixed 2-second (150-frame) lead-in to every track's start
  before summing. The AccurateRip IDs do not.
- Because `StartFrame` is index 1 and `EndFrame` runs to just before the next
  track's index 1, **a track's ripped audio includes the following track's
  pregap.** That's why the log hardcodes `Gap handling: Appended to previous
  track` -- it's a description of the arithmetic, not a configurable setting.

### External tools misbehave in specific, known ways

- `cd-paranoia` writes **all** of its output, including progress and its
  `--version` banner, to **stderr**, not stdout.
- `cdrdao` with no arguments prints its version banner as the first line of
  usage text to stderr and **exits 1**. `SystemInfo.GetCdrdaoVersion` therefore
  can't require a zero exit code, unlike every other version probe.
- `flac --version` behaves normally (stdout, exit 0).
- **cd-paranoia upstream bug:** a read offset above `MaxSafeOffsetSamples`
  (587 samples) can make it misreport the ripped file's size. whatinator warns
  and continues rather than refusing.
- **cd-paranoia upstream bug:** ripping a disc's 99th track may fail outright.
  Also warned about.
- **`--force-overread` support is drive-dependent, and failure isn't clean:**
  manually verified against an ASUS DRW-24F1ST (cdparanoia 10.2/libcdio 2.2.0)
  reading the last few hundred sectors of the final track -- the identical
  span without `--force-overread` completed in seconds, but with it added,
  cd-paranoia neither completed nor errored after 10+ minutes and had to be
  killed. It doesn't fail fast, so don't assume a hang is something else.
  Reproduced twice more directly against the same drive/disc while
  investigating backlog-completed 050: `--force-overread` combined with a
  small but *positive* sample offset (`+6`), reading the disc's **last**
  track, hangs every time; the identical command with `--force-overread`
  dropped succeeds in under a second, every time. Captured output during a
  hang shows it isn't blocked on the drive (not an uninterruptible I/O wait)
  -- it's a tight user-space spin emitting `##: 6 [skip] @ 0` over 1000
  times/second forever, i.e. cd-paranoia's own overread-boundary arithmetic
  has produced a bogus word-offset of `0` and it never detects that and
  bails out. That combination -- read past the official end of the disc,
  positive offset, right at the lead-out -- is exactly what upstream's own
  man page warns can cause "read errors on most drives and possibly even
  hard lockups on some buggy hardware," so treat this as: the *feature*
  legitimately asks a drive to do something outside its guaranteed contract
  (firmware-dependent whether that's honored, refused, or mishandled), but
  the *infinite silent spin with zero forward progress* is squarely
  cd-paranoia's own robustness gap -- a well-behaved client would detect a
  bad computed position and fail instead of looping on it forever. This is
  exactly why `--overread`/`WhatinatorConfig.Overreads` are opt-in rather
  than defaulted on -- see backlog 044. Overreading only ever matters for
  the single track touching the disc's physical edge that the read offset
  shifts into (last track for positive, first for negative); `OverreadPolicy.ResolveBoundaryTrackNumber`
  is what `WhatinatorRipRunner.RipAsync` uses to scope `--force-overread` to
  just that one track instead of passing it to every track uniformly (a
  no-op on any track that isn't at the edge, but no reason to exercise the
  flag where it can't matter -- see backlog-completed 053). `rip`/`pipeline
  --stall-timeout` (`CdParanoiaTrackOptions.StallTimeoutSeconds`,
  `StallMonitor`) is the general mitigation for exactly this class of hang:
  it kills a single cd-paranoia invocation once it reports no forward
  progress for that long, rather than waiting on it forever -- see
  backlog-completed 050. Once that boundary track's overread attempt
  stalls, `--skip-overread-on-stall` (`CdParanoiaTrackOptions.SkipOverreadOnStall`)
  is what decides what happens next: without it, the track is marked
  degraded rather than burning the rest of `MaxRetries` retrying something
  already proven to fail the same way every time; with it,
  `CdParanoiaTrackReader.ReadTrackAsync` drops `Overread` for the track's
  remaining attempts and finishes with the boundary silence-filled instead
  -- see backlog-completed 053. `--max-sector-reads`/`--never-skip` does
  **not** help with any of this -- that caps how many times cd-paranoia
  retries a sector it recognizes as bad, but this spin never increments that
  counter at all; it's a different code path.
- Subprocess stdout/stderr are drained through `ProcessOutputRelay` while the
  process runs. Don't switch to a `WaitForExit()`-then-read shape -- that
  deadlocks on tools that fill the pipe buffer, which `cd-paranoia` will.

### Verification is two independent mechanisms

Don't collapse them:

1. **Test/copy** (`CdParanoiaTrackReader`): rip each track twice to separate temp
   files and compare CRC32s. Catches a drive or drive-cache handing back
   different bytes on different reads. Purely local, works offline, retries the
   whole two-read cycle up to `MaxRetries`.
2. **AccurateRip** (`AccurateRipClient`): one whole-disc lookup after all tracks
   are read, comparing v1/v2 checksums against other people's rips. Catches a
   consistently-wrong read (e.g. wrong offset). Needs the network and needs the
   disc to be in the database.

A track that exhausts its retries is **degraded**, not fatal -- the disc still
packages, with a warning. `WhatinatorRipResult.Degraded` and
`CdParanoiaTrackResult.Degraded` carry that.

### Packaging is idempotent by rescan

`FlacPackager` and `Mp3Packager` move this disc's files in, then regenerate the
container-level artifacts (`releaseinfo.json`, `id.txt`, `checksum_sha256.txt`,
`.m3u`, cover art) by **rescanning whatever is currently on disk**. So they're
safe to run once per disc of a multi-disc release, in any order, across separate
sessions. Preserve that property in any change: never derive a container-level
artifact solely from the disc currently being packaged.

The one exception is `FlacPackager`'s `.cue` sheet (`CueSheetFile`): it's a
genuine per-disc artifact, not a container-level one, and needs that call's
own `DiscToc` (a physical fact a directory rescan can't recover) -- see
`FlacPackageOptions.Toc`. `Mp3Packager` never writes one; see its class doc
comment for why.

Corollary: a degraded disc still contributes its present tracks to the `.m3u`
rather than being omitted until complete.

### ISRC/UPC are physical facts, threaded per call, not stored on `ReleaseInfo`

`DiscToc.CatalogNumber` (UPC/EAN) and `DiscTocTrack.Isrc` reach the rip log
(`WhatinatorEacLog`), FLAC tags (`FlacEncodeOptions.Isrc`), and `id.txt`'s
`upc:` line (`IdTextFile.Format`'s `upc` parameter, threaded through
`FlacPackageOptions.DiscCatalogNumber`) directly from the disc's `DiscToc` on
each call -- they are **not** added to `ReleaseInfo`/`releaseinfo.json`.
That file is purely MusicBrainz/Discogs editorial data (see "the two disc
models" above), and adding a per-disc physical fact to it would violate the
"packaging is idempotent by rescan" invariant just above: a multi-disc
release only ever has one `ReleaseInfo` object, so there'd be no correct
single value to store there for a fact that's read per physical disc.

The same shape of fact: whether a release's MusicBrainz match came from a
disc-ID lookup or a manual release-URL override entered at the picker
(`make-releaseinfo`/`pipeline`'s ambiguous-match and no-match prompts both
offer `m` for this). Threaded the same way, as `bool? DiscIdMatched` through
`PipelineDiscOptions`/`FlacPackageOptions`/`ReleasePackageArtifacts.Write`,
annotating `id.txt`'s MusicBrainz URL line (`(disc-id match)` vs.
`(manual override -- not disc-id matched)`) rather than living on
`ReleaseInfo`. A manual MusicBrainz override is rejected -- and the picker
re-shown -- if the resolved release's track count doesn't match the disc's;
a manual Discogs override has no such check, since `DiscogsInfo` never
carries a track listing in this app.

### The rip log is moved, the MP3 log is regenerated

`FlacPackager` moves the `.log` `WhatinatorRipRunner` wrote **byte for byte,
untouched** -- it's a record of what happened during extraction and must not be
rewritten. `Mp3LogFile`, by contrast, is genuinely regenerated on every MP3 run,
because MP3s can be encoded from a packaged FLAC folder at any time, on any
machine. That's also why `SystemInfo` captures OS/tool versions fresh at
MP3-encode time instead of inheriting them from the rip.

### Console output prefixing

`RipOutputTimestamp.Prefix()` (`yyyyMMdd-HHmmss: `) goes on output *once a rip is
underway*: from the `starting: ...` announcement through each track's read.
It deliberately does **not** go on the MusicBrainz/Discogs selection prompts or
the TOC/ISRC startup section. Keep new output on the correct side of that line.

### Drive identity is not the device path

`readOffsets`, `cacheDefeats`, and `overreads` in the config are keyed by
`WhatinatorConfig.DriveKey(vendor, model, release)` -> `"ASUS|DRW-24F1ST   b|1.00"`,
**not** by `/dev/sr1`. A read offset is a property of the physical drive; which
`/dev/sr*` node it enumerates as can change across boots, and this dev machine
has two optical drives. Keys written before the firmware-revision field was added
have an empty trailing segment.

### Naming rules

- `ReleaseFolderNaming.SortArtist` reorders a leading `"The "` to
  `"Sugarcubes, The"` **for container folder names only**. `ReleaseInfo.Artist`,
  tags, `id.txt`, and the `.m3u` file name all keep the original word order.
- `ExtractYear` falls back to `"0000"` rather than failing.
- `TrackFileNaming` emits `"{NN} - {Title}"`, or `"{NN} - {Artist} - {Title}"`
  when *any* track on *any* disc of the release has an artist differing from the
  release artist (the various-artists case). `TrackFileMatcher` parses only the
  leading digits back out, so the separator style is free to change.
- Everything user-supplied goes through `FileNameSanitizer` before touching the
  filesystem.

### Not supported, on purpose

- **CD-TEXT**: `TocFileParser` skips `CD_TEXT` blocks wholesale. There is no
  consumer for that data.
- **Data tracks are never ripped.** They're skipped with a warning (but still
  count toward the CDDB disc ID and the leadout position -- see above).
- No WAV editing, no CD playback/prelisten, no LP/radio recording, no CD
  burning, no normalization, no glitch removal. These are EAC features the
  project deliberately does not chase.
