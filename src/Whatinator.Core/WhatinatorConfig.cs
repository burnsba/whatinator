using Whatinator.Core.Drive;

namespace Whatinator.Core;

/// <summary>User-configurable defaults for whatinator, loaded via <see cref="ConfigLoader"/>.</summary>
/// <param name="Device">
/// The default optical drive device path to use when <c>--device</c> isn't
/// given on the command line.
/// </param>
/// <param name="MakeMp3">
/// Whether to create MP3s by default during the rip pipeline (phase 007+),
/// unless overridden by a command-line flag.
/// </param>
/// <param name="UserAgent">
/// Overrides the HTTP <c>User-Agent</c> sent with every MusicBrainz/Discogs/
/// Cover Art Archive/AccurateRip request. <see langword="null"/> (the
/// default) means <see cref="EffectiveUserAgent"/> computes
/// <see cref="WhatinatorUserAgent.Default"/> instead -- a user only needs
/// this key to substitute their own contact address.
/// </param>
/// <param name="ReadOffsets">
/// Each known drive's sample read offset, keyed by <see cref="DriveKey"/>
/// (<c>vendor|model|release</c>) -- a read offset is a property of the
/// physical drive, not of whichever <c>/dev/sr*</c> node it happens to
/// enumerate as on a given boot (this dev machine has two optical drives).
/// Replaces phase 012's flat, single-drive <c>ReadOffset</c> stopgap. Phase
/// 017 (<c>whatinator offset-find</c>) is what actually automates
/// populating this map; until then, a drive's entry is added by hand-editing
/// the config file. <c>release</c> (firmware revision) is read via
/// <see cref="OpticalDriveLocator"/> as of phase 016 -- every key built
/// before that phase had an empty <c>release</c> segment.
/// </param>
/// <param name="CacheDefeats">
/// Each known drive's <see cref="CacheDefeatResult"/>, keyed the same way
/// as <see cref="ReadOffsets"/> -- phase 016. <see cref="CacheDefeatAnalyzer"/>'s
/// <c>cd-paranoia -A</c> analysis takes real drive time (a full read/timing
/// pass over the whole disc), so it's a value looked up here rather than
/// run automatically before every rip; <c>whatinator cache-check</c>
/// populates a drive's entry by running the analysis once, same pattern as
/// <see cref="ReadOffsets"/>'s <c>offset-find</c>.
/// </param>
/// <param name="Overreads">
/// Each known drive's <c>--force-overread</c> support, keyed the same way as
/// <see cref="ReadOffsets"/>/<see cref="CacheDefeats"/>. Not every drive
/// handles overreading past the lead-out cleanly -- some error, some return
/// silence -- so this is opt-in per drive rather than defaulted on; an entry
/// is added by hand-editing the config file once a drive's behavior has been
/// manually verified. <c>rip</c>/<c>pipeline</c>'s <c>--overread</c> flag
/// forces it on for a single run regardless of this map.
/// </param>
/// <param name="MaxSectorReads">
/// The default per-sector retry cap passed to cd-paranoia's <c>--never-skip</c>
/// (see <see cref="Rip.CdParanoiaTrackOptions.MaxSectorReads"/>), used when
/// <c>rip</c>/<c>pipeline</c>'s <c>--max-sector-reads</c> isn't given.
/// <see langword="null"/> means the CLI's own hardcoded default (12)
/// applies. Not drive-keyed, unlike <see cref="ReadOffsets"/>/<see cref="Overreads"/> --
/// this is a read-robustness preference, not a physical drive property.
/// </param>
/// <param name="StallTimeoutSeconds">
/// The default stall timeout in seconds (see
/// <see cref="Rip.CdParanoiaTrackOptions.StallTimeoutSeconds"/>), used when
/// <c>rip</c>/<c>pipeline</c>'s <c>--stall-timeout</c> isn't given.
/// <see langword="null"/> means the CLI's own hardcoded default (1200)
/// applies.
/// </param>
public sealed record WhatinatorConfig(
    string Device = "/dev/sr1",
    bool MakeMp3 = true,
    string? UserAgent = null,
    IReadOnlyDictionary<string, int>? ReadOffsets = null,
    IReadOnlyDictionary<string, CacheDefeatResult>? CacheDefeats = null,
    IReadOnlyDictionary<string, bool>? Overreads = null,
    int? MaxSectorReads = null,
    int? StallTimeoutSeconds = null)
{
    /// <summary>
    /// The HTTP <c>User-Agent</c> to send with outbound requests: the
    /// configured <see cref="UserAgent"/> if set, otherwise
    /// <see cref="WhatinatorUserAgent.Default"/>.
    /// </summary>
    public string EffectiveUserAgent => UserAgent ?? WhatinatorUserAgent.Default;

    /// <summary>Builds the <see cref="ReadOffsets"/>/<see cref="CacheDefeats"/> key for a drive's vendor/model/release strings.</summary>
    /// <param name="vendor">The drive's vendor string (e.g. <c>ASUS</c>), or <see langword="null"/> if unknown.</param>
    /// <param name="model">The drive's model string (e.g. <c>DRW-24F1ST</c>), or <see langword="null"/> if unknown.</param>
    /// <param name="release">The drive's firmware revision, or <see langword="null"/> if unknown/not read.</param>
    /// <returns>A stable key, e.g. <c>"ASUS|DRW-24F1ST|1.00"</c>.</returns>
    public static string DriveKey(string? vendor, string? model, string? release = null) =>
        $"{vendor}|{model}|{release}";

    /// <summary>Looks up a drive's known read offset by its vendor/model/release strings.</summary>
    /// <param name="vendor">The drive's vendor string, or <see langword="null"/> if unknown.</param>
    /// <param name="model">The drive's model string, or <see langword="null"/> if unknown.</param>
    /// <param name="release">The drive's firmware revision, or <see langword="null"/> if unknown/not read.</param>
    /// <returns>The configured offset, or <see langword="null"/> if this drive has no entry in <see cref="ReadOffsets"/>.</returns>
    public int? GetReadOffset(string? vendor, string? model, string? release = null) =>
        ReadOffsets is not null && ReadOffsets.TryGetValue(DriveKey(vendor, model, release), out var offset)
            ? offset
            : null;

    /// <summary>Looks up a drive's known cache-defeat result by its vendor/model/release strings.</summary>
    /// <param name="vendor">The drive's vendor string, or <see langword="null"/> if unknown.</param>
    /// <param name="model">The drive's model string, or <see langword="null"/> if unknown.</param>
    /// <param name="release">The drive's firmware revision, or <see langword="null"/> if unknown/not read.</param>
    /// <returns>The configured result, or <see cref="CacheDefeatResult.Unknown"/> if this drive has no entry in <see cref="CacheDefeats"/>.</returns>
    public CacheDefeatResult GetCacheDefeat(string? vendor, string? model, string? release = null) =>
        CacheDefeats is not null && CacheDefeats.TryGetValue(DriveKey(vendor, model, release), out var result)
            ? result
            : CacheDefeatResult.Unknown;

    /// <summary>Looks up whether a drive is known to support <c>--force-overread</c>, by its vendor/model/release strings.</summary>
    /// <param name="vendor">The drive's vendor string, or <see langword="null"/> if unknown.</param>
    /// <param name="model">The drive's model string, or <see langword="null"/> if unknown.</param>
    /// <param name="release">The drive's firmware revision, or <see langword="null"/> if unknown/not read.</param>
    /// <returns><see langword="true"/> only if this drive has a <see langword="true"/> entry in <see cref="Overreads"/>; <see langword="false"/> otherwise, including when this drive has no entry at all.</returns>
    public bool GetOverread(string? vendor, string? model, string? release = null) =>
        Overreads is not null && Overreads.TryGetValue(DriveKey(vendor, model, release), out var overread) && overread;
}
