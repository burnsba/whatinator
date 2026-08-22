# MetadataUpdater rename collides silently after already rewriting files

**Status:** not started

## Description

`src/Whatinator.Core/Metadata/MetadataUpdater.cs:112-129` calls

```csharp
Directory.Move(trimmed, newPath);
```

with no check for whether `newPath` already exists.

## Failure scenario

The user corrects a release year via `update-metadata`, and a correctly-named
folder for that year already exists in the destination (e.g. they ran the
correction once before, or ripped the same release twice). `Directory.Move`
throws a raw `IOException`.

The damage is in the ordering: by that point `releaseinfo.json`, `id.txt`, and
`checksum_sha256.txt` have **already been rewritten**. The folder is left
half-migrated -- new metadata inside, old name outside -- and the exception
message does not explain what happened or what state things are in.

`releaseinfo.bak` exists and can revert the metadata, but the user has to know
that, and nothing in the error says so.

## Acceptance Criteria

- [ ] `Directory.Exists(newPath)` checked **before** any writes; fail early with a
      message naming both the existing folder and the intended new name.
- [ ] The exception message mentions `releaseinfo.bak` as the revert path.
- [ ] Ideally, validate every precondition up front so `update-metadata` either
      completes fully or changes nothing.
- [ ] New test: `MetadataUpdaterTests` currently has ten tests and none for a
      pre-existing destination folder. Add one asserting the operation fails
      before mutating the source folder.
