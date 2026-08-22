namespace Whatinator.Core;

/// <summary>Writes EXTM3U playlists, matching <c>example/*.m3u</c>'s format.</summary>
public static class M3uPlaylist
{
    /// <summary>Writes an EXTM3U playlist listing the given tracks in order.</summary>
    /// <param name="tracks">Each track's relative file path, artist, title, and duration in seconds.</param>
    /// <param name="m3uPath">The destination playlist file path.</param>
    public static void Write(
        IEnumerable<(string RelativePath, string Artist, string Title, int DurationSeconds)> tracks,
        string m3uPath)
    {
        ArgumentNullException.ThrowIfNull(tracks);

        var lines = new List<string> { "#EXTM3U" };
        foreach (var track in tracks)
        {
            lines.Add($"#EXTINF:{track.DurationSeconds},{track.Artist} - {track.Title}");
            lines.Add(track.RelativePath);
        }

        File.WriteAllLines(m3uPath, lines);
    }
}
