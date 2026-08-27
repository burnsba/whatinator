namespace Whatinator.Core.Rip;

/// <summary>One attempt's outcome within <see cref="CdParanoiaTrackReader.RetryAsync"/>'s bounded retry loop.</summary>
internal enum TrackAttemptOutcome
{
    /// <summary>The attempt succeeded -- the loop stops and reports success.</summary>
    Matched,

    /// <summary>The attempt failed; the loop retries again if attempts remain.</summary>
    Failed,

    /// <summary>
    /// The attempt failed in a way further retries are known not to fix
    /// (e.g. an overread stall with no <c>--skip-overread-on-stall</c>) --
    /// the loop stops immediately rather than exhausting the remaining
    /// retry budget on something that will just fail the same way again.
    /// </summary>
    GiveUp,
}
