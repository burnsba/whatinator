# PipelineRunner has almost no test coverage over its path arithmetic

**Status:** not started

## Description

`Rip/PipelineRunnerTests.cs` has only two tests, both covering disc-number
validation. The class's own doc comment acknowledges the gap.

The untested part that matters is the multi-disc `rawDir` /
`eventualDiscDirectory` computation at `PipelineRunner.cs:100-107`. That code has
to **duplicate `FlacPackager`'s own path logic** in order to write the rip log
with correct per-track `Filename` lines -- the log is written before the
packager moves the files, so the runner must predict where they will end up.

Duplicated path arithmetic that must agree with another class, with no test
asserting they agree, is exactly the kind of thing that drifts silently. A
mismatch would produce a rip log whose `Filename` lines point at paths that do
not exist -- cosmetically fine, archivally wrong, and invisible until someone
reads an old log.

## Acceptance Criteria

- [ ] New test: for a multi-disc release, `PipelineRunner`'s predicted
      `eventualDiscDirectory` equals the `DiscDirectory` that `FlacPackager`
      actually produces for the same inputs. This is the agreement test.
- [ ] New test: same for a single-disc release (flat layout, no `cdN/`).
- [ ] New test: the rip log's `Filename` lines resolve to files that exist after
      packaging completes.
- [ ] Better: eliminate the duplication -- have both call one shared path
      resolver -- at which point the agreement test becomes structural rather than
      behavioural. Fits naturally with the packager-consolidation item.
- [ ] New test: `--no-flac` leaves raw output in place and skips packaging.
