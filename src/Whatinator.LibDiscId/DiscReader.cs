using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Whatinator.LibDiscId;

/// <summary>Reads disc TOC information via the native libdiscid library.</summary>
/// <remarks>
/// Linux-only: the bound native library is <c>libdiscid.so.0</c>, which can
/// only resolve via the Debian/Ubuntu <c>libdiscid0</c> package (or another
/// distribution's equivalent) on Linux. See the project <c>CLAUDE.md</c>.
/// </remarks>
[SupportedOSPlatform("linux")]
public static class DiscReader
{
    /// <summary>
    /// The message shown when the native <c>libdiscid</c> shared library
    /// cannot be resolved by the dynamic linker at all -- as opposed to
    /// resolving but failing a specific operation (e.g. no disc in the
    /// drive), which produces its own <see cref="DiscIdException"/> from
    /// <c>discid_get_error_msg</c> instead.
    /// </summary>
    private const string MissingLibraryMessage =
        "libdiscid is not installed, or is not on the dynamic linker's search path. "
        + "Install it with: sudo apt install libdiscid0 (Debian/Ubuntu) or your "
        + "distribution's libdiscid package.";

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
    /// <exception cref="DiscIdException">
    /// The native read failed -- e.g. no disc in the drive -- or the native
    /// <c>libdiscid</c> shared library could not be resolved at all.
    /// </exception>
    public static Disc Read(string device, DiscIdFeatures features = DiscIdFeatures.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(device);

        try
        {
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
        catch (DllNotFoundException ex)
        {
            throw WrapMissingLibrary(ex);
        }
    }

    /// <summary>Returns the platform's default optical drive device path, as reported by libdiscid.</summary>
    /// <returns>The default device path.</returns>
    /// <exception cref="DiscIdException">
    /// libdiscid did not report a default device, or the native <c>libdiscid</c>
    /// shared library could not be resolved at all.
    /// </exception>
    public static string GetDefaultDevice()
    {
        try
        {
            return Marshal.PtrToStringUTF8(NativeMethods.discid_get_default_device())
                ?? throw new DiscIdException("libdiscid did not report a default device.");
        }
        catch (DllNotFoundException ex)
        {
            throw WrapMissingLibrary(ex);
        }
    }

    /// <summary>Returns the version string of the underlying native libdiscid library (e.g. <c>"libdiscid 0.7.0"</c>).</summary>
    /// <returns>The native library's version string.</returns>
    /// <exception cref="DiscIdException">The native <c>libdiscid</c> shared library could not be resolved at all.</exception>
    public static string GetNativeVersion()
    {
        try
        {
            return Marshal.PtrToStringUTF8(NativeMethods.discid_get_version_string()) ?? "unknown";
        }
        catch (DllNotFoundException ex)
        {
            throw WrapMissingLibrary(ex);
        }
    }

    /// <summary>
    /// Translates a <see cref="DllNotFoundException"/> raised when the native
    /// <c>libdiscid</c> shared library can't be resolved into an actionable
    /// <see cref="DiscIdException"/>. Kept as a separate, internally-visible
    /// helper so the translation can be unit-tested without needing an
    /// environment where <c>libdiscid0</c> is actually missing.
    /// </summary>
    /// <param name="ex">The exception thrown by the failed P/Invoke marshalling.</param>
    /// <returns>The <see cref="DiscIdException"/> to throw in its place.</returns>
    internal static DiscIdException WrapMissingLibrary(DllNotFoundException ex) =>
        new(MissingLibraryMessage, ex);

    /// <summary>Marshals a native UTF-8 string owned by libdiscid, throwing if it was unexpectedly null.</summary>
    /// <param name="ptr">The native pointer returned by a <c>discid_get_*</c> function.</param>
    /// <param name="what">A short description of the value, used in the exception message.</param>
    private static string RequireString(IntPtr ptr, string what)
    {
        return Marshal.PtrToStringUTF8(ptr)
            ?? throw new DiscIdException($"libdiscid did not return a {what}.");
    }
}
