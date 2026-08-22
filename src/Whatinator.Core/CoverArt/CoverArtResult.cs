namespace Whatinator.Core.CoverArt;

/// <summary>The result of a successful cover art download.</summary>
/// <param name="Content">The raw image bytes.</param>
/// <param name="FileExtension">A file extension (including the leading dot, e.g. <c>".jpg"</c>) inferred from the response's content type.</param>
public sealed record CoverArtResult(byte[] Content, string FileExtension);
