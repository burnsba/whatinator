namespace Whatinator.LibDiscId;

/// <summary>
/// Thrown when a native <c>libdiscid</c> operation fails -- for example, no
/// disc in the drive, or the device path doesn't exist.
/// </summary>
public sealed class DiscIdException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DiscIdException"/> class.</summary>
    public DiscIdException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiscIdException"/> class.</summary>
    /// <param name="message">A description of what went wrong.</param>
    public DiscIdException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiscIdException"/> class.</summary>
    /// <param name="message">A description of what went wrong.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public DiscIdException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
