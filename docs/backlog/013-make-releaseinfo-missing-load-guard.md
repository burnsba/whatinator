# make-releaseinfo is the one command missing the releaseinfo load guard

**Status:** not started

## Description

`src/Whatinator.Cli/MakeReleaseInfoCommand.cs:33-36` calls
`ReleaseInfoFile.Load(releaseInfoPath)` bare.

Every other command that loads a releaseinfo wraps it in the same filter:

```csharp
catch (Exception ex) when (ex is IOException or JsonException)
```

- `IdTxtCommand.cs:26-33`
- `RipCommand.cs:46-53`
- `FlacCommand.cs:46-53`
- `Mp3Command.cs:126-133`
- `PipelineCommand.cs:140-148`
- `UpdateMetadataCommand.cs:147-156`

`make-releaseinfo` is the sole omission -- six near-identical copies and one that
forgot, which is the classic consequence of copy-paste duplication (see the CLI
duplication backlog item).

## Failure scenario

```
whatinator make-releaseinfo --releaseinfo typo.json
```

produces an unhandled `FileNotFoundException` and a stack trace, where the
identical typo against `id-txt` produces the clean
`Failed to read typo.json: ...`. Same for a corrupt JSON file.

## Related

`MakeReleaseInfoCommand.cs:48-50`'s `Directory.CreateDirectory(dest)` /
`ReleaseInfoFile.Save` are also unguarded -- as are `IdTxtCommand.cs:35-37` and
`PipelineCommand.cs:161-162`. `UnauthorizedAccessException` does **not** derive
from `IOException`, so the existing filters do not cover a read-only destination
anywhere in the CLI.

## Acceptance Criteria

- [ ] `make-releaseinfo` uses the same catch filter as the other six commands.
- [ ] `UnauthorizedAccessException` added to the filter at all write sites listed
      above.
- [ ] Better: a shared `TryLoadReleaseInfo` helper so there is one copy rather
      than seven (see the CLI duplication backlog item) -- this defect is the
      argument for doing that.
- [ ] New test: `make-releaseinfo --releaseinfo <missing path>` exits non-zero with
      a one-line message and no stack trace.
