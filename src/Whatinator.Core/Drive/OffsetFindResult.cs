namespace Whatinator.Core.Drive;

/// <summary>The outcome of <see cref="OffsetFinder.FindAsync"/>.</summary>
/// <param name="Offset">The found offset, or <see langword="null"/> if not found.</param>
/// <param name="FailureReason">Why no offset was found, or <see langword="null"/> on success.</param>
public sealed record OffsetFindResult(int? Offset, OffsetFindFailureReason? FailureReason)
{
    /// <summary>Whether an offset was found.</summary>
    public bool Found => Offset is not null;
}
