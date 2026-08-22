---
name: work-item
description: Work a whatinator backlog item end to end -- plan, branch, implement, document, and stage for review. Triggers on "work on <backlog item>", "pick up backlog item N", "let's do this ad-hoc: <request>", or any request to implement a change to whatinator from either docs/backlog or a fresh ask.
---

# Work item

Drives one unit of work in whatinator from backlog (or ad-hoc request) through
staged-for-review changes on a feature branch. Follow these steps in order;
don't skip or reorder them.

## 1. Identify the work item

- If the user names a `docs/backlog/NNN-*.md` file or gives enough detail to
  find one, read it in full.
- If the user describes ad-hoc work with no matching backlog file, create a new
  `docs/backlog/NNN-slug.md` first, following the structure of existing items
  (title, `**Status:** not started`, `## Description`, `## Acceptance Criteria`).
  Use the next free `NNN` number (zero-padded, three digits) -- check both
  `docs/backlog/` and `docs/backlog-completed/` for the highest existing number.
  This file is the plan's starting point, not a formality -- write it with the
  same care as the existing entries.

## 2. Refresh the codebase index

Before reading any code, make sure `codebase-memory-mcp`'s index is current:
call `mcp__codebase-memory-mcp__index_status` (or `detect_changes`) and run
`index_repository` if it's stale or missing. Do this even if you expect the
index to already be fresh -- it's cheap and stale results are worse than a
short wait.

## 3. Build an implementation plan

- Read the backlog item and use `search_graph` / `trace_path` /
  `get_code_snippet` / `get_architecture` to understand the affected code
  before proposing changes -- don't rely on assumptions about structure.
- Cross-check against the relevant `CLAUDE.md` files (root, and
  `src/Whatinator.Core/CLAUDE.md`, `src/Whatinator.LibDiscId/CLAUDE.md` if the
  work touches those projects) for conventions and gotchas that bear on the
  approach.
- Draft a concrete plan: which files change, what the new behavior is, how it
  will be tested.
- Use `EnterPlanMode` for this -- it's how implementation plans get approved in
  this environment.

## 4. Ask about ambiguity, then wait if you did

If anything in the backlog item or ad-hoc request is genuinely ambiguous (an
open decision the item itself flags, an unstated preference between two valid
approaches), ask the user with `AskUserQuestion` before finalizing the plan.

- If you asked anything: wait for the user's answers and fold them into the
  plan before proceeding to implementation.
- If nothing was ambiguous: proceed directly, no need to pause for a rubber
  stamp.

## 5. Create and switch to a feature branch

Once the plan is set (and approved, if questions were asked), create a feature
branch from the current branch and switch to it, so the user can follow the
work on a separate branch. Name it after the backlog item, e.g.
`work/019-cli-option-docs-mismatch`. Confirm the working tree is clean before
branching (`git status`) -- if it isn't, stop and ask rather than branching
over unrelated uncommitted work.

## 6. Implement

- Make the planned changes. When you need to understand existing code before
  touching it, prefer `codebase-memory-mcp` queries (`search_graph`,
  `trace_path`, `get_code_snippet`, `search_code`) over blind file reads.
- Follow the conventions in the relevant `CLAUDE.md` files (doc comments on
  every member, `ConfigureAwait(false)`, records vs. sealed classes, interfaces
  as test seams, etc.) -- these are load-bearing project rules, not style
  suggestions.

## 7. Definition of done

Before considering the change complete, verify all of the following:

- [ ] Source code doc comments updated to reflect the change (every public,
      internal, and private member needs one -- see root `CLAUDE.md`).
- [ ] If a command's behavior or options changed: `HelpContent.cs` updated to
      match.
- [ ] If the README documents the changed behavior (or should): README updated
      to match. Keep `HelpContent.cs` and the README command tables in sync --
      they drift apart easily.
- [ ] If any touched `CLAUDE.md` needs updating: update it, but keep additions
      minimal -- only the most important, non-derivable facts belong there, not
      a narration of the change.
- [ ] If core business logic changed (not glue/CLI plumbing): a test case
      exists covering it, in the matching `*.Tests` project.
- [ ] `dotnet build` and `dotnet test` pass (note if any test failures are
      pre-existing/environmental, e.g. missing `ffprobe`/`sox`/`magick`, per
      root `CLAUDE.md`).

## 8. Close out the backlog item

- Update the item's `**Status:**` line to `done`.
- Move the file from `docs/backlog/` to `docs/backlog-completed/` (`git mv`).

## 9. Bump the version

Increase `<Version>` in `Directory.Build.props` by 0.0.1 (patch bump), unless
the user has said otherwise. This is the single source of truth for the
version -- don't add a version literal anywhere else.

## 10. Stage for review

`git add` the changed files on the feature branch and leave them staged --
**do not commit**. The user reviews the staged diff manually before deciding
whether to commit. Report a short summary of what changed and point out
anything from step 7 that couldn't be completed (e.g., no test coverage
possible, README left as-is because nothing user-facing changed).
