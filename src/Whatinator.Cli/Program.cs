using Microsoft.Extensions.DependencyInjection;
using Whatinator.Cli;
using Whatinator.Core;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.CoverArt;
using Whatinator.Core.Discogs;
using Whatinator.Core.MusicBrainz;

// Ctrl-C is otherwise a hard process kill: cd-paranoia/cdrdao/flac/lame keep
// running (holding the drive or a scratch file open) and no rip log gets
// written. Cancelling this token instead lets every layer down to the
// subprocess wrappers unwind cleanly -- see root CLAUDE.md § Gotchas and
// docs/backlog-completed/004-ctrl-c-orphans-subprocesses.md.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();

    // Safety net for a case the cooperative CancellationToken can't reach:
    // Console.In.ReadLineAsync(token) (the MusicBrainz/Discogs picker's
    // manual-URL-override prompt -- see ConsolePicker.cs and
    // MakeReleaseInfoCommand.PromptManualMusicBrainzOverrideAsync/
    // PromptManualDiscogsOverrideAsync) blocks on a real terminal's
    // canonical-mode read(), which the .NET runtime cannot actually
    // interrupt once a line has unterminated characters in it (e.g. a
    // pasted URL before Enter is pressed) -- confirmed by direct repro:
    // the token cancels and this handler runs, but the pending read never
    // unblocks. Every other Ctrl-C path (mid-rip, mid-prompt-with-no-
    // partial-line) already exits well inside this window via the normal
    // cooperative unwind, so this only ever fires for that one stuck case.
    _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(
        _ => Environment.Exit(130),
        TaskScheduler.Default);
};

try
{
    return await CliExceptionBoundary.RunAsync(
        () => RunWithServicesAsync(args, cts.Token),
        Console.Error,
        showStackTrace: IsDebugEnabled(args));
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}

// --debug (or WHATINATOR_DEBUG) re-enables the full stack trace that
// CliExceptionBoundary otherwise replaces with a one-line message; see
// docs/backlog-completed/005-no-top-level-exception-handler.md. Must come
// after the command name, like every other flag -- args[0] still has to be
// the command for CommandDispatcher's switch to match.
static bool IsDebugEnabled(string[] args) =>
    args.Contains("--debug") || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WHATINATOR_DEBUG"));

// Loads config and wires up the named HttpClients the commands resolve via
// IHttpClientFactory -- BaseAddress and the User-Agent header are configured
// once here, not by the client constructors themselves (MusicBrainzClient,
// DiscogsClient, CoverArtClient, AccurateRipClient all treat the HttpClient
// they're given as caller-owned and caller-configured, and mutating it a
// second time would throw once a request has gone out or double the
// User-Agent header). Kept inside the closure CliExceptionBoundary wraps,
// not at file top-level, so a broken config file's JsonException still gets
// the boundary's one-line error message instead of crashing before it's
// installed.
static async Task<int> RunWithServicesAsync(string[] args, CancellationToken cancellationToken)
{
    var config = ConfigLoader.Load();

    var services = new ServiceCollection();
    services.AddHttpClient("musicbrainz", c =>
    {
        c.BaseAddress = new Uri(MusicBrainzClient.BaseUrl);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(config.EffectiveUserAgent);
    });
    services.AddHttpClient("discogs", c =>
    {
        c.BaseAddress = new Uri(DiscogsClient.BaseUrl);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(config.EffectiveUserAgent);
    });
    services.AddHttpClient("coverart", c =>
    {
        c.BaseAddress = new Uri(CoverArtClient.BaseUrl);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(config.EffectiveUserAgent);
    });
    services.AddHttpClient("accuraterip", c =>
    {
        c.BaseAddress = new Uri(AccurateRipClient.BaseUrl);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(config.EffectiveUserAgent);
    });

    using var serviceProvider = services.BuildServiceProvider();
    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

    return await CommandDispatcher.RunAsync(args, httpClientFactory, cancellationToken).ConfigureAwait(false);
}
