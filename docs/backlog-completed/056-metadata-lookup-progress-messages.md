# Metadata lookup: print progress before network requests and on auto-selected matches

**Status:** done

## Description

Observed running `pipeline --device /dev/sr1 --dest ... --overread
--skip-overread-on-stall`: after the TOC read, the CLI printed nothing at all
for a noticeable stretch, then suddenly asked the user to choose among 7
Discogs releases. Nothing on screen indicated a MusicBrainz or Discogs lookup
was in flight during that gap -- it looked like a hang.

`MakeReleaseInfoCommand.LookUpFromDiscAsync` (shared by both
`make-releaseinfo` and `pipeline` -- see root `CLAUDE.md`'s data-flow
section) makes three kinds of network calls with no console output before
them:

1. `service.LookupByDiscIdAsync(disc.Id, cancellationToken)` -- the initial
   MusicBrainz disc-ID lookup (`MakeReleaseInfoCommand.cs:117`).
2. `service.ResolveAsync(releaseId, cancellationToken)` inside
   `ResolveAndValidateTrackCountAsync` (`MakeReleaseInfoCommand.cs:321`) --
   the full-release fetch after the user picks a candidate from the
   ambiguous-match picker, or after a manual MusicBrainz URL override.
3. `discogsClient.SearchByBarcodeAsync(releaseInfo.Barcode, cancellationToken)`
   inside `EnrichWithDiscogsAsync` (`MakeReleaseInfoCommand.cs:389`) -- the
   Discogs barcode search.

Separately, when any of these lookups resolves to exactly one match, the
match is used automatically with zero console output -- the single-candidate
case in `MetadataService.LookupByDiscIdAsync`
(`src/Whatinator.Core/Metadata/MetadataService.cs:47-49`, `Found` status) and
in `EnrichWithDiscogsAsync`'s `candidates.Count switch` (`case 1 =>
candidates[0]`). The user has no way to tell a selection happened
automatically, or what it resolved to, until much later output (or the
packaged folder's `id.txt`) reveals it.

Per root `CLAUDE.md`'s Core design rules ("No console I/O in Core" --
`Whatinator.Core/CLAUDE.md`), all of this output belongs in
`Whatinator.Cli/MakeReleaseInfoCommand.cs`, not in `MetadataService`,
`MusicBrainzClient`, or `DiscogsClient`.

## Acceptance Criteria

- [ ] A status line is printed to console immediately before each network
      lookup listed above: the initial MusicBrainz disc-ID lookup, the
      full-release fetch after a picker selection or manual override, and the
      Discogs barcode search. (The manual-URL-override fetches already sit
      right after an interactive prompt the user just answered, but get a
      status line too for consistency.)
- [ ] When `MetadataService.LookupByDiscIdAsync` returns `Found` (exactly one
      disc-ID candidate, auto-resolved), print the resolved release's
      identifying details (artist, title, date, country, catalog number,
      barcode -- matching the style `DescribeMusicBrainzCandidate` already
      uses) so the user can see what was picked without being asked.
- [ ] When `EnrichWithDiscogsAsync`'s barcode search returns exactly one
      candidate, print its details (reusing `DescribeDiscogsCandidate`) before
      using it automatically.
- [ ] No console I/O added to `Whatinator.Core` -- all new output lives in
      `Whatinator.Cli`.
- [ ] `dotnet build` and `dotnet test` pass.
