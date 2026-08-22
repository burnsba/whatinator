namespace Whatinator.Core.Drive;

/// <summary>The outcome of <see cref="CacheDefeatAnalyzer.AnalyzeAsync"/> -- whether the drive lets cd-paranoia defeat its audio cache.</summary>
public enum CacheDefeatResult
{
    /// <summary>cd-paranoia couldn't determine an answer (e.g. no disc in the drive).</summary>
    Unknown,

    /// <summary>The drive's audio cache can be defeated.</summary>
    CanDefeat,

    /// <summary>The drive's audio cache cannot be defeated.</summary>
    CannotDefeat,
}
