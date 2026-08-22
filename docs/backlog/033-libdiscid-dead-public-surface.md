# Disc.SubmissionUrl and Disc.TocString are populated but never used

**Status:** not started

## Description

`src/Whatinator.LibDiscId/Disc.cs:15-16`

Both are populated on **every** read (`DiscReader.cs:54-55`, two native calls per
read) and referenced nowhere outside the library. `DiscInfoFormatter.Print`
(`DiscInfoFormatter.cs:13-22`) uses only `Id`, `FreedbId`, `FirstTrack`,
`LastTrack`, and `Tracks`.

The cost is negligible -- two pointer reads and two small string copies. The
problem is that they are unexercised by any test and will silently rot: if
libdiscid ever changes their format, nothing would notice.

`SubmissionUrl` in particular has obvious future value (submitting an unmatched
disc's TOC to MusicBrainz is a natural feature for this tool), so deleting it may
be the wrong call.

## Acceptance Criteria

Pick one:

**Keep:**
- [ ] Test added asserting both are non-empty and well-formed for a known TOC
      (`SubmissionUrl` parses as a URI and contains the disc ID).
- [ ] Doc comment notes they are currently unused and why they are retained.

**Drop:**
- [ ] Both removed from `Disc`, and the two `discid_get_*` calls removed from
      `DiscReader.Read`.
- [ ] A note recorded that `discid_get_submission_url` is trivially re-bindable if
      a "submit unmatched disc to MusicBrainz" feature is ever wanted.
