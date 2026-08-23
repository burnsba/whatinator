using Microsoft.Extensions.DependencyInjection;
using Whatinator.Cli;
using Whatinator.Core;

var services = new ServiceCollection();

services.AddHttpClient();

using var serviceProvider = services.BuildServiceProvider();

var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

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
};

try
{
    return await CliExceptionBoundary.RunAsync(
        () => CommandDispatcher.RunAsync(args, httpClientFactory, cts.Token),
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
