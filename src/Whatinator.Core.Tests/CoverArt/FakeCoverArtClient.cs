using Whatinator.Core.CoverArt;
using Whatinator.Core.Flac;

namespace Whatinator.Core.Tests;

/// <summary>An in-memory <see cref="ICoverArtClient"/> stand-in for testing <see cref="FlacPackager"/> without real network access.</summary>
internal sealed class FakeCoverArtClient : ICoverArtClient
{
    private readonly CoverArtResult? _result;

    public FakeCoverArtClient(CoverArtResult? result = null)
    {
        _result = result;
    }

    public int CallCount { get; private set; }

    public Task<CoverArtResult?> TryDownloadFrontCoverAsync(string musicBrainzReleaseId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_result);
    }
}
