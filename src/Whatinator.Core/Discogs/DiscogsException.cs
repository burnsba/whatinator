namespace Whatinator.Core.Discogs;

/// <summary>
/// Thrown when a Discogs API request fails -- network error, non-success
/// HTTP status, or a response that couldn't be parsed.
/// </summary>
public sealed class DiscogsException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DiscogsException"/> class.</summary>
    public DiscogsException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiscogsException"/> class.</summary>
    /// <param name="message">A description of what went wrong.</param>
    public DiscogsException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiscogsException"/> class.</summary>
    /// <param name="message">A description of what went wrong.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public DiscogsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
