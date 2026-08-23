using Microsoft.Extensions.DependencyInjection;
using Whatinator.Cli;

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
    return await CommandDispatcher.RunAsync(args, httpClientFactory, cts.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}
