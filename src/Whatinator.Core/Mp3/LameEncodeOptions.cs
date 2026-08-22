namespace Whatinator.Core.Mp3;

/// <summary>Options for one track's <see cref="LameEncoder.EncodeAsync"/> invocation.</summary>
/// <param name="InputFlacPath">The source FLAC file (this system's <c>lame</c> decodes FLAC directly -- no separate decode step).</param>
/// <param name="OutputMp3Path">The destination MP3 file.</param>
/// <param name="Title">The track title (<c>--tt</c>).</param>
/// <param name="Artist">The track artist (<c>--ta</c>).</param>
/// <param name="Album">The release title (<c>--tl</c>).</param>
/// <param name="AlbumArtist">The release artist, set via the <c>TPE2</c> frame (lame has no dedicated short flag for album artist).</param>
/// <param name="Year">The release year (<c>--ty</c>), or <see langword="null"/> if unknown.</param>
/// <param name="TrackNumber">The 1-based track number (<c>--tn</c>).</param>
/// <param name="TrackCount">The disc's total track count, appended to <c>--tn</c> as <c>N/total</c>.</param>
/// <param name="Genre">The genre (<c>--tg</c>), or <see langword="null"/> if unknown.</param>
public sealed record LameEncodeOptions(
    string InputFlacPath,
    string OutputMp3Path,
    string Title,
    string Artist,
    string Album,
    string AlbumArtist,
    string? Year,
    int TrackNumber,
    int TrackCount,
    string? Genre);
