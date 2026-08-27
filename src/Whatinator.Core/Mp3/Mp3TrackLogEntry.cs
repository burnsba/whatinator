namespace Whatinator.Core.Mp3;

/// <summary>One track's section of the MP3 log, written by <see cref="Mp3LogFile"/>.</summary>
/// <param name="TrackNumber">The track's position within this encoding run (1-based, not necessarily the disc's track number -- matches <see cref="Mp3Packager"/>'s own <c>Track N of M</c> progress line).</param>
/// <param name="TotalTracks">How many tracks this run is encoding.</param>
/// <param name="Title">The track's title.</param>
/// <param name="LameOutput"><c>lame</c>'s filtered final-summary output for this track (see <see cref="LameEncodeResult.CapturedOutput"/>).</param>
public sealed record Mp3TrackLogEntry(int TrackNumber, int TotalTracks, string Title, string LameOutput);
