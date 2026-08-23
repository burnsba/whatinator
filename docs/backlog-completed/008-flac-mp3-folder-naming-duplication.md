# FlacFolderNaming and Mp3FolderNaming are identical apart from the format tag

**Status:** done

## Description

`src/Whatinator.Core/Naming/FlacFolderNaming.cs:15-22` and
`src/Whatinator.Core/Naming/Mp3FolderNaming.cs:15-22` are byte-identical apart
from one token:

```csharp
// FlacFolderNaming
return FileNameSanitizer.Sanitize($"{sortArtist} - {releaseInfo.Title} [flac {year}]");

// Mp3FolderNaming
return FileNameSanitizer.Sanitize($"{sortArtist} - {releaseInfo.Title} [mp3 v0 {year}]");
```

Everything else -- the `ArgumentNullException.ThrowIfNull`, the
`ReleaseFolderNaming.ExtractYear` call, the `ReleaseFolderNaming.SortArtist`
call, the doc comments -- is the same. The genuinely shared half was already
factored into `ReleaseFolderNaming`; only the trivial remaining half got copied.

## Proposed fix

One method on `ReleaseFolderNaming`, which is already the documented home for
"shared building blocks for release output folder/file names":

```csharp
public static string ContainerFolderName(ReleaseInfo releaseInfo, string formatTag)
```

Callers pass `"flac"` / `"mp3 v0"`. The format tag belongs with the packager
that owns it.

Three call sites:

- `Flac/FlacPackager.cs:56`
- `Mp3/Mp3Packager.cs:62`
- `Metadata/MetadataUpdater.cs:114-116` -- currently branches on file extension to
  choose between the two classes; collapses to
  `ContainerFolderName(info, extension == ".flac" ? "flac" : "mp3 v0")`.

Note `ReleaseFolderNaming.SortArtist`'s doc comment cross-references
`FlacFolderNaming.ContainerFolderName` and `Mp3FolderNaming.ContainerFolderName`
by `<see cref="..."/>`; those references need updating or the build will fail
(doc-comment enforcement is on).

## Acceptance Criteria

- [ ] `FlacFolderNaming.cs` and `Mp3FolderNaming.cs` deleted.
- [ ] `ReleaseFolderNaming.ContainerFolderName(releaseInfo, formatTag)` added,
      with a doc comment listing the tags in use and noting that the tag is the
      caller's (the packager's) concern.
- [ ] All three call sites updated; `MetadataUpdater`'s extension branch collapsed.
- [ ] `<see cref="..."/>` references in `ReleaseFolderNaming.SortArtist` updated.
- [ ] `Naming/FlacFolderNamingTests.cs` (4 tests) and `Naming/Mp3FolderNamingTests.cs`
      (3 tests) merged into a single `[Theory]` over the format tag, preserving
      every existing case -- including the `"The "` reordering and the `0000`
      year fallback.
- [ ] Output folder names are byte-identical to before the change (verify against
      an existing packaged release folder).
