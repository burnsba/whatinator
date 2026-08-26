using Whatinator.Core.Metadata;

namespace Whatinator.Cli;

/// <summary>
/// The outcome of <see cref="MakeReleaseInfoCommand.LookUpFromDiscAsync"/>:
/// the resolved release, plus whether it came from a MusicBrainz disc-ID
/// match or a manual URL override. <see cref="PipelineCommand"/> and
/// <see cref="MakeReleaseInfoCommand"/> both need the latter to annotate
/// <c>id.txt</c> correctly (see <c>docs/backlog-completed/052-metadata-picker-manual-override-and-ctrlc.md</c>)
/// -- see root <c>CLAUDE.md</c> § "ISRC/UPC are physical facts, threaded per
/// call, not stored on <c>ReleaseInfo</c>" for why this isn't just a field on
/// <see cref="ReleaseInfo"/> itself.
/// </summary>
/// <param name="ReleaseInfo">The resolved release.</param>
/// <param name="DiscIdMatched">
/// <see langword="true"/> if <see cref="ReleaseInfo"/> was picked from
/// disc-ID-matched candidates (including the single-match auto-resolved
/// case); <see langword="false"/> if it came from a manual release-URL
/// override instead.
/// </param>
internal sealed record ResolvedRelease(ReleaseInfo ReleaseInfo, bool DiscIdMatched);
