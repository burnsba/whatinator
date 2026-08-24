# WavFile has no coverage for the odd-size chunk pad byte or a truncated data chunk

**Status:** done

## Description

`Whatinator.Core.Tests/AccurateRip/WavFileTests.cs` has four tests. Neither of
the two riskiest branches in `WavFile` is among them:

- The **odd-size chunk pad byte** (`WavFile.cs:76-79`). RIFF chunks of odd length
  are followed by a pad byte. The implementation handles this correctly, but
  nothing asserts it, so a refactor could silently break chunk walking on any
  file with an odd-sized `LIST`/`INFO` chunk before `data`.
- A **truncated `data` chunk** -- a header declaring more bytes than the file
  contains. This is the realistic corruption mode for a rip interrupted mid-write
  (which the cancellation backlog item shows is currently easy to produce).

`WavFile.ReadDataChunk` is what feeds `AccurateRipChecksum`, so a
misparse silently corrupts every checksum on the disc. Its "throw, don't degrade"
contract is documented and appropriate; it just needs the branches pinned.

## Acceptance Criteria

- [ ] New test: a WAV with an odd-sized chunk preceding `data` parses correctly
      and returns the right PCM bytes.
- [ ] New test: a WAV whose `data` chunk header declares more bytes than are
      present throws with a diagnosable message rather than returning short data.
- [ ] New test: a WAV with no `data` chunk at all throws.
- [ ] New test: a non-RIFF file throws.
