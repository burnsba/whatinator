namespace Whatinator.Core.AccurateRip;

/// <summary>
/// One parsed AccurateRip database response entry (one community
/// submission for a disc) -- the confidence/checksum data
/// <see cref="AccurateRipClient.MatchAsync"/> matches against. Exposed
/// directly via <see cref="IAccurateRipEntryLookup.GetEntriesAsync"/> for
/// <see cref="Drive.OffsetFinder"/>'s incremental per-track checksum
/// probing (it needs to test one track's checksum against every entry at a
/// time, before it has computed checksums for the rest of the disc --
/// unlike <see cref="AccurateRipClient.MatchAsync"/>, which needs every
/// audio track's checksum computed up front). Was previously an
/// <c>internal</c> record nested only in <see cref="AccurateRipClient"/>'s
/// own file; promoted to its own public file once <see cref="Drive.OffsetFinder"/>
/// needed to see it too.
/// </summary>
/// <param name="Confidences">Per-track confidence, indexed by relative audio-track position (HTOA excluded).</param>
/// <param name="Checksums">Per-track CRC, indexed the same way as <paramref name="Confidences"/>.</param>
public sealed record AccurateRipDbEntry(byte[] Confidences, uint[] Checksums);
