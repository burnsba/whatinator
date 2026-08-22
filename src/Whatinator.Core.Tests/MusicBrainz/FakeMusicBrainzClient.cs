using Whatinator.Core.Metadata;
using Whatinator.Core.MusicBrainz;

namespace Whatinator.Core.Tests;

/// <summary>An in-memory <see cref="IMusicBrainzClient"/> stand-in for testing <see cref="MetadataService"/> without real network access.</summary>
internal sealed class FakeMusicBrainzClient : IMusicBrainzClient
{
    private readonly IReadOnlyList<ReleaseCandidate> _candidates;
    private readonly Dictionary<string, ReleaseInfo> _releasesById;
    private int _getReleaseCallCount;

    public FakeMusicBrainzClient(IReadOnlyList<ReleaseCandidate> candidates, IReadOnlyList<ReleaseInfo> releases)
    {
        _candidates = candidates;
        _releasesById = releases.ToDictionary(r => r.MusicBrainzReleaseId);
    }

    public int GetReleaseCallCount => _getReleaseCallCount;

    public Task<IReadOnlyList<ReleaseCandidate>> LookupByDiscIdAsync(string discId) => Task.FromResult(_candidates);

    public Task<ReleaseInfo> GetReleaseAsync(string releaseId)
    {
        _getReleaseCallCount++;
        return Task.FromResult(_releasesById[releaseId]);
    }
}
