using System.Runtime.InteropServices;

namespace Whatinator.LibDiscId;

/// <summary>
/// Raw P/Invoke declarations for the subset of the native <c>libdiscid</c> C
/// API (<c>libdiscid.so.0</c>) this project uses. Callers should go through
/// <see cref="DiscReader"/> rather than calling these directly.
/// </summary>
/// <remarks>
/// Signatures were sourced from the stable, long-unchanged <c>discid.h</c> C
/// ABI and cross-checked with <c>nm -D</c> against the installed
/// <c>libdiscid.so.0.7.0</c> -- no <c>libdiscid-dev</c> package (and thus no
/// local header) is installed on the dev machine. See the project
/// <c>CLAUDE.md</c> for the full list of exported functions and which are
/// out of scope.
/// </remarks>
internal static partial class NativeMethods
{
    /// <summary>
    /// The library name passed to the P/Invoke marshaller. Resolves directly
    /// via the dynamic linker's SONAME lookup on Debian-family Linux (package
    /// <c>libdiscid0</c>) without needing a full path.
    /// </summary>
    private const string LibraryName = "libdiscid.so.0";

    /// <summary>Allocates a new native <c>DiscId</c> instance.</summary>
    /// <returns>A handle owning the newly allocated instance.</returns>
    [LibraryImport(LibraryName)]
    internal static partial DiscIdSafeHandle discid_new();

    /// <summary>Frees a native <c>DiscId</c> instance. Called by <see cref="DiscIdSafeHandle"/>.</summary>
    /// <param name="disc">The raw native pointer to free.</param>
    [LibraryImport(LibraryName)]
    internal static partial void discid_free(IntPtr disc);

    /// <summary>
    /// Reads the TOC (and, depending on <paramref name="features"/>, MCN/ISRC
    /// data) from <paramref name="device"/>.
    /// </summary>
    /// <param name="disc">The instance to populate.</param>
    /// <param name="device">The device path to read, e.g. <c>/dev/sr1</c>.</param>
    /// <param name="features">A bitwise combination of <see cref="DiscIdFeatures"/> values.</param>
    /// <returns>1 on success, 0 on failure (call <see cref="discid_get_error_msg"/> for details).</returns>
    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int discid_read_sparse(DiscIdSafeHandle disc, string device, uint features);

    /// <summary>Returns the MusicBrainz disc ID. Owned by <paramref name="disc"/>; do not free.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <returns>A native UTF-8 string pointer.</returns>
    [LibraryImport(LibraryName)]
    internal static partial IntPtr discid_get_id(DiscIdSafeHandle disc);

    /// <summary>Returns the FreeDB disc ID. Owned by <paramref name="disc"/>; do not free.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <returns>A native UTF-8 string pointer.</returns>
    [LibraryImport(LibraryName)]
    internal static partial IntPtr discid_get_freedb_id(DiscIdSafeHandle disc);

    /// <summary>Returns the MusicBrainz TOC submission URL. Owned by <paramref name="disc"/>; do not free.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <returns>A native UTF-8 string pointer.</returns>
    [LibraryImport(LibraryName)]
    internal static partial IntPtr discid_get_submission_url(DiscIdSafeHandle disc);

    /// <summary>Returns the raw TOC string. Owned by <paramref name="disc"/>; do not free.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <returns>A native UTF-8 string pointer.</returns>
    [LibraryImport(LibraryName)]
    internal static partial IntPtr discid_get_toc_string(DiscIdSafeHandle disc);

    /// <summary>Returns the number of the first audio track.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <returns>The first track number.</returns>
    [LibraryImport(LibraryName)]
    internal static partial int discid_get_first_track_num(DiscIdSafeHandle disc);

    /// <summary>Returns the number of the last audio track.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <returns>The last track number.</returns>
    [LibraryImport(LibraryName)]
    internal static partial int discid_get_last_track_num(DiscIdSafeHandle disc);

    /// <summary>Returns the total sector count (the leadout track's offset).</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <returns>The total sector count.</returns>
    [LibraryImport(LibraryName)]
    internal static partial int discid_get_sectors(DiscIdSafeHandle disc);

    /// <summary>Returns the start offset, in CDDA sectors (75/sec), of the given track.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <param name="trackNumber">The 1-based track number.</param>
    /// <returns>The track's start offset in sectors.</returns>
    [LibraryImport(LibraryName)]
    internal static partial int discid_get_track_offset(DiscIdSafeHandle disc, int trackNumber);

    /// <summary>Returns the length, in CDDA sectors (75/sec), of the given track.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <param name="trackNumber">The 1-based track number.</param>
    /// <returns>The track's length in sectors.</returns>
    [LibraryImport(LibraryName)]
    internal static partial int discid_get_track_length(DiscIdSafeHandle disc, int trackNumber);

    /// <summary>Returns the error message for the most recent failed operation on <paramref name="disc"/>.</summary>
    /// <param name="disc">The instance to read from.</param>
    /// <returns>A native UTF-8 string pointer.</returns>
    [LibraryImport(LibraryName)]
    internal static partial IntPtr discid_get_error_msg(DiscIdSafeHandle disc);

    /// <summary>Returns the platform's default optical drive device path.</summary>
    /// <returns>A native UTF-8 string pointer.</returns>
    [LibraryImport(LibraryName)]
    internal static partial IntPtr discid_get_default_device();

    /// <summary>Returns the version string of the native libdiscid library (e.g. <c>"libdiscid 0.7.0"</c>).</summary>
    /// <returns>A native UTF-8 string pointer.</returns>
    [LibraryImport(LibraryName)]
    internal static partial IntPtr discid_get_version_string();
}
