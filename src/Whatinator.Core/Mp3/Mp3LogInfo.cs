namespace Whatinator.Core.Mp3;

/// <summary>The data captured for one MP3-encoding run, written by <see cref="Mp3LogFile"/>.</summary>
/// <param name="Uname">The <c>uname -a</c> output.</param>
/// <param name="OsPrettyName">The OS's <c>PRETTY_NAME</c> from <c>/etc/os-release</c>, or <see langword="null"/> if unavailable.</param>
/// <param name="LameVersion">The <c>lame --version</c> output (first line).</param>
/// <param name="StartTime">When encoding started.</param>
/// <param name="EndTime">When encoding finished.</param>
/// <param name="Tracks">Each encoded track's captured <c>lame</c> output, in encode order. <see langword="null"/>/empty omits the per-track sections entirely.</param>
public sealed record Mp3LogInfo(
    string Uname,
    string? OsPrettyName,
    string LameVersion,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    IReadOnlyList<Mp3TrackLogEntry>? Tracks = null);
