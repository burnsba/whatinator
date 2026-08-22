namespace Whatinator.Core.Flac;

/// <summary>The outcome of a <see cref="FlacEncoder.EncodeAsync"/> invocation.</summary>
/// <param name="ExitCode">The <c>flac</c> process's exit code.</param>
public sealed record FlacEncodeResult(int ExitCode)
{
    /// <summary>Whether <c>flac</c> exited successfully (<see cref="ExitCode"/> is 0) -- including its own <c>--verify</c> decode-and-compare check.</summary>
    public bool Success => ExitCode == 0;
}
