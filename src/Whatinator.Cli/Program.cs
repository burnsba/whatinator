using Microsoft.Extensions.DependencyInjection;
using Whatinator.Cli;

var services = new ServiceCollection();

services.AddHttpClient();

using var serviceProvider = services.BuildServiceProvider();

var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

return await CommandDispatcher.RunAsync(args, httpClientFactory);
