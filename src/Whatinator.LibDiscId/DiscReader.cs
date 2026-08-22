using System.Runtime.InteropServices;

namespace Whatinator.LibDiscId;

/// <summary>Reads disc TOC information via the native libdiscid library.</summary>
public static class DiscReader
{
    /// <summary>
    /// Reads the TOC of the disc in <paramref name="device"/> and returns its
    /// MusicBrainz disc ID, track listing, and related identifiers.
    /// </summary>
    /// <param name="device">
    /// The device path to read, e.g. <c>/dev/sr1</c>.
    /// </param>
    /// <param name="features">
    /// Which extra data to read beyond the TOC. Defaults to
    /// <see cref="DiscIdFeatures.None"/> -- see that type's remarks for why
    /// that default should generally be left alone.
    /// </param>
    /// <returns>The disc's TOC and identifiers.</returns>
    /// <exception cref="ArgumentException"><paramref name="device"/> is null, empty, or whitespace.</exception>
    /// <exception cref="DiscIdException">The native read failed -- e.g. no disc in the drive.</exception>
    public static Disc Read(string device, DiscIdFeatures features = DiscIdFeatures.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(device);

        using var handle = NativeMethods.discid_new();
        if (handle.IsInvalid)
        {
            throw new DiscIdException("Failed to allocate a native libdiscid DiscId instance.");
        }

        var success = NativeMethods.discid_read_sparse(handle, device, (uint)features);
        if (success == 0)
        {
            var message = Marshal.PtrToStringUTF8(NativeMethods.discid_get_error_msg(handle));
            throw new DiscIdException(message ?? $"libdiscid failed to read '{device}' for an unknown reason.");
        }

        var firstTrack = NativeMethods.discid_get_first_track_num(handle);
        var lastTrack = NativeMethods.discid_get_last_track_num(handle);

        var tracks = new List<Track>(Math.Max(0, lastTrack - firstTrack + 1));
        for (var trackNumber = firstTrack; trackNumber <= lastTrack; trackNumber++)
        {
            var offset = NativeMethods.discid_get_track_offset(handle, trackNumber);
            var length = NativeMethods.discid_get_track_length(handle, trackNumber);
            tracks.Add(new Track(trackNumber, offset, length));
        }

        return new Disc(
            Id: RequireString(NativeMethods.discid_get_id(handle), "disc ID"),
            FreedbId: RequireString(NativeMethods.discid_get_freedb_id(handle), "FreeDB ID"),
            SubmissionUrl: RequireString(NativeMethods.discid_get_submission_url(handle), "submission URL"),
            TocString: RequireString(NativeMethods.discid_get_toc_string(handle), "TOC string"),
            FirstTrack: firstTrack,
            LastTrack: lastTrack,
            Sectors: NativeMethods.discid_get_sectors(handle),
            Tracks: tracks);
    }

    /// <summary>Returns the platform's default optical drive device path, as reported by libdiscid.</summary>
    /// <returns>The default device path.</returns>
    /// <exception cref="DiscIdException">libdiscid did not report a default device.</exception>
    public static string GetDefaultDevice()
    {
        return Marshal.PtrToStringUTF8(NativeMethods.discid_get_default_device())
            ?? throw new DiscIdException("libdiscid did not report a default device.");
    }

    /// <summary>Returns the version string of the underlying native libdiscid library (e.g. <c>"libdiscid 0.7.0"</c>).</summary>
    /// <returns>The native library's version string.</returns>
    public static string GetNativeVersion()
    {
        return Marshal.PtrToStringUTF8(NativeMethods.discid_get_version_string()) ?? "unknown";
    }

    /// <summary>Marshals a native UTF-8 string owned by libdiscid, throwing if it was unexpectedly null.</summary>
    /// <param name="ptr">The native pointer returned by a <c>discid_get_*</c> function.</param>
    /// <param name="what">A short description of the value, used in the exception message.</param>
    private static string RequireString(IntPtr ptr, string what)
    {
        return Marshal.PtrToStringUTF8(ptr)
            ?? throw new DiscIdException($"libdiscid did not return a {what}.");
    }
}
