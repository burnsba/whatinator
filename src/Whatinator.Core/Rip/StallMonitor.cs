namespace Whatinator.Core.Rip;

/// <summary>
/// Polls a <see cref="CdParanoiaProgressReporter"/>'s <see cref="CdParanoiaProgressReporter.TimeSinceProgress"/>
/// while a single cd-paranoia invocation is running and cancels the supplied
/// <see cref="CancellationTokenSource"/> once it exceeds <c>stallTimeout</c> --
/// <see cref="CdParanoiaTrackReader.RunCdParanoiaAsync"/> is what actually
/// kills the process on that cancellation (via <see cref="SubprocessRunner"/>/
/// <see cref="ProcessCancellation"/>); this class only decides when to ask
/// for it. A known cd-paranoia bug can leave a read neither completing nor
/// erroring for the lifetime of the process (see root <c>CLAUDE.md</c> §
/// Gotchas' <c>--force-overread</c> entry) -- this is the safety net for
/// exactly that case, and for a genuinely wedged drive more generally.
/// </summary>
internal sealed class StallMonitor : IDisposable
{
    /// <summary>How often to check for a stall -- frequent enough that the actual kill lands soon after <c>stallTimeout</c> elapses, without being a busy loop.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _stopMonitoring = new();
    private readonly Task _monitorTask;

    /// <summary>Initializes a new instance of the <see cref="StallMonitor"/> class and starts polling immediately.</summary>
    /// <param name="reporter">The progress reporter to poll for <see cref="CdParanoiaProgressReporter.TimeSinceProgress"/>.</param>
    /// <param name="stallTimeout">How long <see cref="CdParanoiaProgressReporter.TimeSinceProgress"/> may grow before <paramref name="cancelOnStall"/> is cancelled.</param>
    /// <param name="cancelOnStall">Cancelled once a stall is detected -- expected to be a token linked to (not the same as) the caller's own cancellation token, so a real stall can be told apart from genuine user cancellation.</param>
    public StallMonitor(CdParanoiaProgressReporter reporter, TimeSpan stallTimeout, CancellationTokenSource cancelOnStall)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(cancelOnStall);

        _monitorTask = PollAsync(reporter, stallTimeout, cancelOnStall, _stopMonitoring.Token);
    }

    /// <summary>Stops polling. Does not itself cancel <c>cancelOnStall</c> -- only a detected stall does that.</summary>
    public void Dispose()
    {
        _stopMonitoring.Cancel();
        try
        {
            _monitorTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected: PollAsync's delay loop observes _stopMonitoring and exits.
        }

        _stopMonitoring.Dispose();
    }

    /// <summary>Polls <paramref name="reporter"/> every <see cref="PollInterval"/>, cancelling <paramref name="cancelOnStall"/> the first time <paramref name="stallTimeout"/> is exceeded.</summary>
    private static async Task PollAsync(CdParanoiaProgressReporter reporter, TimeSpan stallTimeout, CancellationTokenSource cancelOnStall, CancellationToken stopMonitoring)
    {
        while (!stopMonitoring.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, stopMonitoring).ConfigureAwait(false);

            if (reporter.TimeSinceProgress >= stallTimeout)
            {
                cancelOnStall.Cancel();
                return;
            }
        }
    }
}
