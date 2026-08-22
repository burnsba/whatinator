namespace Whatinator.Core.Metadata;

/// <summary>The result of a metadata lookup via <see cref="MetadataService.LookupByDiscIdAsync"/>.</summary>
public sealed class MetadataLookupResult
{
    private MetadataLookupResult(
        MetadataLookupStatus status,
        ReleaseInfo? releaseInfo,
        IReadOnlyList<ReleaseCandidate>? candidates)
    {
        Status = status;
        ReleaseInfo = releaseInfo;
        Candidates = candidates;
    }

    /// <summary>Which of the three outcomes this result represents.</summary>
    public MetadataLookupStatus Status { get; }

    /// <summary>
    /// The fully-resolved release, populated only when <see cref="Status"/>
    /// is <see cref="MetadataLookupStatus.Found"/>.
    /// </summary>
    public ReleaseInfo? ReleaseInfo { get; }

    /// <summary>
    /// The candidate releases needing disambiguation, populated only when
    /// <see cref="Status"/> is <see cref="MetadataLookupStatus.Ambiguous"/>.
    /// </summary>
    public IReadOnlyList<ReleaseCandidate>? Candidates { get; }

    /// <summary>The single shared <see cref="MetadataLookupStatus.NotFound"/> result.</summary>
    public static MetadataLookupResult NotFound { get; } =
        new(MetadataLookupStatus.NotFound, releaseInfo: null, candidates: null);

    /// <summary>Creates a <see cref="MetadataLookupStatus.Found"/> result.</summary>
    /// <param name="releaseInfo">The single, fully-resolved matching release.</param>
    /// <returns>The result.</returns>
    public static MetadataLookupResult Found(ReleaseInfo releaseInfo) =>
        new(MetadataLookupStatus.Found, releaseInfo, candidates: null);

    /// <summary>Creates an <see cref="MetadataLookupStatus.Ambiguous"/> result.</summary>
    /// <param name="candidates">The candidate releases needing disambiguation.</param>
    /// <returns>The result.</returns>
    public static MetadataLookupResult Ambiguous(IReadOnlyList<ReleaseCandidate> candidates) =>
        new(MetadataLookupStatus.Ambiguous, releaseInfo: null, candidates);
}
