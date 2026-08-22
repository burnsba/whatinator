namespace Whatinator.Core.Metadata;

/// <summary>The outcome of a metadata lookup via <see cref="MetadataService.LookupByDiscIdAsync"/>.</summary>
public enum MetadataLookupStatus
{
    /// <summary>No MusicBrainz release matched the disc.</summary>
    NotFound,

    /// <summary>Exactly one release matched; already fully resolved.</summary>
    Found,

    /// <summary>Multiple releases matched; the caller must pick one and call <see cref="MetadataService.ResolveAsync"/>.</summary>
    Ambiguous,
}
