namespace Whatinator.Core.MusicBrainz;

/// <summary>
/// Thrown when a MusicBrainz API request fails -- network error, non-success
/// HTTP status, or a response that couldn't be parsed.
/// </summary>
public sealed class MusicBrainzException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MusicBrainzException"/> class.</summary>
    public MusicBrainzException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MusicBrainzException"/> class.</summary>
    /// <param name="message">A description of what went wrong.</param>
    public MusicBrainzException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MusicBrainzException"/> class.</summary>
    /// <param name="message">A description of what went wrong.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public MusicBrainzException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
