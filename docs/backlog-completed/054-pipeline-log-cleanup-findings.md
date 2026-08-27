# Misc pipeline/log findings from a real `pipeline --overread --skip-overread-on-stall` run

**Status:** done

## Description

Found running `dotnet run --project src/Whatinator.Cli -- pipeline --device
/dev/sr1 --dest ~/Music/eac/ --overread --skip-overread-on-stall` against
"Bob Dylan - Desire". Six separate, unrelated-enough-to-fix-independently
observations:

### 1. Stray `releaseinfo.json` left in `--dest` after packaging

`PipelineCommand.ResolveReleaseInfoAsync` (`PipelineCommand.cs`) writes a
working copy of `releaseinfo.json` directly into `--dest` (e.g.
`~/Music/eac/releaseinfo.json`) before ripping starts, so `FlacPackager`
has something to read. Packaging then writes its own, container-level
`releaseinfo.json` inside `"{SortArtist} - {Title} [flac {Year}]/"` (see
root CLAUDE.md's "packaging is idempotent by rescan"), but nothing ever
removes the top-level working copy afterward. It's left behind in `--dest`
alongside the two release folders. Should be cleaned up once packaging has
its own copy -- but not before *every* disc of a multi-disc release has been
packaged, since `ResolveReleaseInfoAsync` is only called once per pipeline
run and packaging for later discs may still need it.

### 2. FLAC log's "Add ID3 tag" line -- RESOLVED, not a bug

Investigated against the real `flac` binary: `flac --help` has no
ID3-related option at all. `-T`/`--tag` (what `FlacEncoder.BuildStartInfo`
uses) sets FLAC's own native Vorbis-comment block, a wholly different
tagging mechanism from ID3. `Add ID3 tag: No` is already correct.
`example/Bob Dylan - Desire.log`'s "Yes" reflects a real Windows EAC
session's own encoder invocation, not anything whatinator's `FlacEncoder`
does. Left as-is, with a clarifying comment added in
`WhatinatorEacLog.AppendEncoderSettings` explaining why "No" is correct so
the discrepancy against the example log doesn't get "fixed" by a future
reader who hasn't done this research.

### 3. FLAC log is missing track 1's pre-gap length -- RESOLVED, disc-dependent

Investigated against the actual disc used for the original bug report (in
`/dev/sr1`): a real `cdrdao --fast-toc` read of it shows no `PREGAP`/`START`
line for track 1 at all in the raw `.toc` output. Per the existing comment
in `TocFileParser.Parse`, track 1's pregap is read directly off the disc's
own TOC/Q-subchannel data (available even under a fast read) rather than
derived or assumed -- so an absent line means this specific disc genuinely
has no detectable track-1 pregap. `WhatinatorEacLog`'s `if (pregap > 0)`
guard is already correct; the example log's `0:00:02.00` came from a
different disc that does have one. No code change.

### 4. FLAC log's UPC/EAN line when the catalog number is unknown

`AppendCommon` unconditionally prints
`Disc catalogue number (UPC/EAN)             : {catalog ?? "none"}`. Real
EAC omits this line entirely when there's no catalog number rather than
printing a placeholder. Change to only emit the line when
`o.Toc.CatalogNumber` is non-null.

### 5. MP3 log full of raw ANSI/control sequences

`Mp3TrackLogEntry.LameOutput` is `LameEncodeResult.CapturedOutput`, lame's
raw stderr, captured verbatim by `ProcessOutputRelay` in `LameEncoder`. lame
writes its live progress bar using carriage returns plus ANSI cursor/erase
codes (`ESC[K`, `ESC[A`, etc.) to redraw in place on a terminal; captured to
a buffer instead of a live terminal, those bytes survive literally and show
up in the MP3 log as `␛`, `␛[K`, `␛[A` garbage.

### 6. MP3 log captures the entire lame output stream, not just the final summary

Related to #5 but a separate fix: even with control codes stripped, the
captured text is *every* progress-bar redraw lame wrote during the encode,
when only the final one-time summary block (post-encode stats: bitrate,
ReplayGain, etc.) is wanted in the log. Needs filtering down to the
meaningful final lines, not just stripping escape sequences from the noise.

## Acceptance Criteria

- [x] #1: the working-copy `releaseinfo.json` written to `--dest` by
      `PipelineCommand` no longer remains in `--dest` once every disc in the
      run has been packaged (single- and multi-disc cases both covered) --
      `PipelineCommand.CleanUpWorkingReleaseInfo`, gated on `!noFlac` and the
      run covering the whole release (`startDisc == 1 && endDisc ==
      releaseInfo.Media.Count`).
- [x] #2: `Add ID3 tag` log line verified against what `flac`'s `-T` flags
      actually do -- confirmed correct as "No"; clarifying comment added,
      no behavior change.
- [x] #3: verified against the real disc used for the report -- this disc
      has no track-1 pregap at all in cdrdao's own TOC data, so omitting the
      line is correct; not a bug, no code change.
- [x] #4: `Disc catalogue number (UPC/EAN)` line is omitted entirely when
      `DiscToc.CatalogNumber` is `null`, matching EAC --
      `WhatinatorEacLog.AppendHeader`.
- [x] #5: MP3 log entries contain no raw ANSI/control-character sequences --
      `LameOutputFilter.ExtractSummary`, applied in `LameEncoder.EncodeAsync`.
- [x] #6: MP3 log entries contain only lame's final summary output per
      track (bitrate histogram + kbps/MS/% line + tag/ReplayGain), not every
      intermediate progress redraw -- same fix as #5.
- [x] New/updated tests: `WhatinatorEacLogTests` (#4 -- omission case plus
      the existing presence case), new `LameOutputFilterTests` (#5/#6,
      including a fail-open case), `LameEncoderTests` updated for the new
      filtered `CapturedOutput` shape. `PipelineCommand.cs` has no test
      coverage per root `CLAUDE.md` (individual `*Command.cs` files need a
      drive/network) -- #1 and #4 verified manually instead via a real
      `pipeline` run against the disc in `/dev/sr1` (`--releaseinfo` to
      skip the picker); #5/#6 verified manually via a real `mp3` run
      against an already-packaged FLAC folder ripped from that same disc.
