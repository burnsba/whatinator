namespace Whatinator.Core.Mp3;

/// <summary>The outcome of a <see cref="LameEncoder.EncodeAsync"/> invocation.</summary>
/// <param name="ExitCode">The <c>lame</c> process's exit code.</param>
/// <param name="CapturedOutput">The raw text lame wrote to stderr (its stdout is always empty), for the MP3 log.</param>
public sealed record LameEncodeResult(int ExitCode, string CapturedOutput)
{
    /// <summary>Whether <c>lame</c> exited successfully (<see cref="ExitCode"/> is 0).</summary>
    public bool Success => ExitCode == 0;
}
