namespace Whatinator.Core.Flac;

/// <summary>Options for one track's <see cref="FlacEncoder.EncodeAsync"/> invocation.</summary>
/// <param name="InputWavPath">The source WAV file (a <see cref="Whatinator.Core.Rip.CdParanoiaTrackReader"/>-accepted track read).</param>
/// <param name="OutputFlacPath">The destination FLAC file.</param>
/// <param name="Title">The track title (<c>-T TITLE=</c>).</param>
/// <param name="Artist">The track artist (<c>-T ARTIST=</c>).</param>
/// <param name="Album">The release title (<c>-T ALBUM=</c>).</param>
/// <param name="AlbumArtist">The release artist (<c>-T ALBUMARTIST=</c>).</param>
/// <param name="Year">The release year (<c>-T DATE=</c>), or <see langword="null"/> if unknown.</param>
/// <param name="TrackNumber">The 1-based track number (<c>-T TRACKNUMBER=</c>).</param>
/// <param name="TrackCount">The disc's total track count (<c>-T TRACKTOTAL=</c>).</param>
/// <param name="Genre">The genre (<c>-T GENRE=</c>), or <see langword="null"/> if unknown.</param>
public sealed record FlacEncodeOptions(
    string InputWavPath,
    string OutputFlacPath,
    string Title,
    string Artist,
    string Album,
    string AlbumArtist,
    string? Year,
    int TrackNumber,
    int TrackCount,
    string? Genre);
