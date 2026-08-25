# CdParanoiaProgressLine.TryParse can throw OverflowException

**Status:** done

## Description

`src/Whatinator.Core/Rip/CdParanoiaProgressLine.cs:34` uses `int.Parse` on an
unbounded `\d+` regex capture. A garbage or absurdly large offset in
cd-paranoia's progress output throws `OverflowException`.

A method named `TryParse` should not throw. The exception escapes from **both**
consumers:

- `CdParanoiaProgressReporter.Feed` -- killing the progress relay task mid-rip.
  Because the relay task's exception is not observed (see the cancellation
  backlog item), this could manifest as progress silently stopping rather than as
  a reported error.
- `ComputeQuality` -- aborting quality computation for the track.

The input is untrusted in the relevant sense: it is text scraped from another
program's stderr, which may be interleaved with other output or truncated.

## Acceptance Criteria

- [ ] `int.TryParse` used; a value that does not parse causes `TryParse` to return
      `false` rather than throwing.
- [ ] The regex bounds the digit count, or the parse failure path is explicitly
      tested.
- [ ] New test: a progress line with an offset exceeding `int.MaxValue` returns
      `false` and does not throw.
- [ ] New test: a truncated or interleaved progress line is rejected cleanly.
