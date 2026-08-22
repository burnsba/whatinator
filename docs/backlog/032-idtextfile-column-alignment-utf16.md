# id.txt duration column misaligns for titles with surrogate pairs

**Status:** not started

## Description

`src/Whatinator.Core/IdTextFile.cs:154,161` aligns the duration column using
`string.Length`, i.e. **UTF-16 code units**.

Titles containing emoji or other astral-plane characters (surrogate pairs, 2 code
units for 1 glyph) or combining marks are measured wrongly, so the duration
column does not line up.

`init.md` states the alignment requirement explicitly -- pad the duration so it
forms an aligned column, unless doing so would exceed 80 characters -- so this is
a stated requirement, not an incidental nicety. Purely cosmetic in effect.

## Acceptance Criteria

- [ ] Width computed from text elements rather than UTF-16 code units
      (`StringInfo` / `EnumerateRunes`), or a documented decision that code units
      are close enough and why.
- [ ] The 80-character cap from `init.md` still respected.
- [ ] New tests in `IdTextFileTests`: a title containing an emoji and a title
      containing a combining accent both align correctly.
- [ ] Verify against `example/id.txt` that ordinary ASCII output is byte-identical
      to before.
