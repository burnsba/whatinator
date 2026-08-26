# Metadata picker: Ctrl-C to abort, show catalog number, manual URL override

**Status:** done

## Description

Prompted by a real disc (Bob Dylan - *Desire*, US pressing, catalog `CK33893`,
barcode `074643389327`) whose exact pressing has no MusicBrainz disc ID
submitted for it, so `LookupByDiscIdAsync` never surfaces
`https://musicbrainz.org/release/0586779a-1e0c-465a-ad7d-5dd1c0946028` even
though it's the correct release. The only way to point whatinator at it today
is `make-releaseinfo --releaseinfo <file>` with a hand-authored file, or
picking the closest of the disc-ID candidates and accepting wrong metadata.
Several smaller gaps around the picker surfaced at the same time.

### 1. Ctrl-C during `ConsolePicker.PromptForSelection` doesn't visibly exit

`Program.cs` wires `Console.CancelKeyPress` to a `CancellationTokenSource`
(see root `CLAUDE.md` § Gotchas, and `docs/backlog-completed/004`), but
`ConsolePicker.PromptForSelection`
(`src/Whatinator.Cli/ConsolePicker.cs:24-63`) blocks on a bare
`Console.ReadLine()` with no `cancellationToken` parameter at all. Nothing
subprocess-related is running while the prompt is up, so Ctrl-C should abort
immediately rather than requiring a second Ctrl-C or SIGINT's default
behavior. `PromptForSelection` needs a `CancellationToken` parameter (both
call sites in `MakeReleaseInfoCommand.cs` already have one in scope) and a
way to unblock the blocking `ReadLine()` on cancellation -- e.g. registering
on the token to close `Console.In` / signal off a `Console.KeyAvailable` poll
loop instead of a flat `ReadLine()`.

### 2. Picker doesn't show catalog number

`ReleaseCandidate` already carries `CatalogNumber` (populated in
`MusicBrainzClient.ToCandidate`), but
`MakeReleaseInfoCommand.DescribeMusicBrainzCandidate` (line 209) only prints
barcode:

```csharp
$"{candidate.Artist} - {candidate.Title} ({candidate.Date ?? "?"}, {candidate.Country ?? "?"}) barcode={candidate.Barcode ?? "?"}";
```

Add `cat={candidate.CatalogNumber ?? "?"}`, matching the style
`DescribeDiscogsCandidate` already uses for its own `cat=` field. This is the
detail that would have let the picker line for the correct release be spotted
by catalog number even without a barcode match -- and its absence made it
harder to notice that the wanted release just wasn't in the disc-ID candidate
set at all, since barcode was the only field ever shown.

### 3. No manual override to specify a release directly

Both the MusicBrainz ambiguous-picker and the Discogs
multiple-match-picker need an escape hatch: a `m` ("manual") choice that
prompts for a release URL and resolves against that instead of the disc-ID
(or barcode-search) candidate list.

- MusicBrainz: parse the MBID out of a pasted
  `https://musicbrainz.org/release/<mbid>` URL and resolve it via the
  already-existing `IMusicBrainzClient.GetReleaseAsync(releaseId, ...)` /
  `MetadataService.ResolveAsync` (`src/Whatinator.Core/Metadata/MetadataService.cs:63-64`)
  -- no new fetch-by-ID plumbing needed, just URL parsing and wiring the
  picker's `m` branch to it instead of an index.
- Discogs: parse a release ID out of a pasted
  `https://www.discogs.com/release/<id>-...` URL. Check whether
  `IDiscogsClient` already exposes (or needs) a fetch-by-ID method --
  currently it only has `SearchByBarcodeAsync`
  (`src/Whatinator.Core/Discogs/DiscogsClient.cs:49`); a manual override needs
  a direct "fetch this exact release" call.
- **Track count must match.** After resolving the manually-specified release,
  compare its total track count (`releaseInfo.Media.Sum(m => m.Tracks.Count)`,
  same computation `MakeReleaseInfoCommand.RunAsync` already does at line 76)
  against the disc's actual TOC audio-track count. On mismatch, print an
  error explaining the counts didn't match and return to the release-selection
  prompt (re-show the numbered list + `m` option) rather than aborting the
  command or silently accepting a wrong release.
- `ConsolePicker.PromptForSelection` is shared generic infrastructure used by
  both pickers (`allowSkip` already exists as a similar cross-cutting
  option) -- decide whether the manual-override support belongs as another
  flag/callback on the shared picker, or as a wrapper each call site composes
  around it. The MusicBrainz and Discogs override flows parse different URL
  shapes and call different resolve methods, so the shared piece is probably
  just "the loop accepts a non-numeric `m` and hands control to a
  caller-supplied delegate," not the URL parsing itself.

### 4. `id.txt` doesn't say whether the MusicBrainz match came from disc ID

`IdTextFile.Format` (`src/Whatinator.Core/IdTextFile.cs:41-70`) always prints
`releaseInfo.MusicBrainzUrl` unconditionally, with no indication of *how*
that release was chosen. Once a manual override path exists, that's a real
ambiguity: a reader of `id.txt` (or of the packaged folder months later)
can't tell whether the MusicBrainz release was matched by disc ID (the
"this is provably the right disc" case) or hand-picked/overridden (a human
judgment call, potentially on a pressing whose TOC doesn't match this disc at
all). Add a short annotation next to the MusicBrainz URL line -- e.g. a
trailing `(disc-id match)` vs. `(manual override -- not disc-id matched)` --
sourced from a new flag threaded alongside `ReleaseInfo`/through
`FlacPackageOptions` the same way `DiscCatalogNumber`/`upc` already is (see
root `CLAUDE.md` § "ISRC/UPC are physical facts, threaded per call, not
stored on `ReleaseInfo`" for the precedent -- this is the same shape of
problem: a fact about *how this call resolved metadata*, not an editorial
fact belonging on `ReleaseInfo` itself). Do **not** put it on `ReleaseInfo`/
`releaseinfo.json` for the same reason UPC isn't: a multi-disc release has
one `ReleaseInfo`, but disc-ID-match status could in principle differ by
disc (unlikely for MusicBrainz, whose releases are usually matched once, but
avoid baking in the assumption).

## Acceptance Criteria

- [ ] `ConsolePicker.PromptForSelection` takes a `CancellationToken` and
      returns/unblocks promptly on Ctrl-C during either the MusicBrainz or
      Discogs prompt, without leaving the process to be killed by a second
      signal.
- [ ] `DescribeMusicBrainzCandidate` includes `cat={candidate.CatalogNumber ?? "?"}`.
- [ ] Both pickers offer an `m` manual-override choice that prompts for a
      release URL (MusicBrainz release URL / Discogs release URL
      respectively), parses the ID out of it, and fetches that exact release.
- [ ] A manually-overridden MusicBrainz release whose total track count
      doesn't match the disc's audio track count prints a clear error and
      returns to the release-selection prompt rather than proceeding or
      aborting.
- [ ] `id.txt`'s MusicBrainz URL line distinguishes a disc-ID match from a
      manual override, and the "how was this resolved" fact is threaded
      per-call rather than stored on `ReleaseInfo`.
- [ ] New tests: `ConsolePicker` cancellation behavior, the manual-override
      URL parsing (both good and malformed URLs) for MusicBrainz and Discogs,
      the track-count mismatch/retry loop, and `IdTextFile.Format`'s two
      annotation variants.
- [ ] Root `CLAUDE.md` and any touched project `CLAUDE.md`/README updated per
      the repo's Definition of Done.
