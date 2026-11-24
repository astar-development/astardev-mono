using AStarDev.CloudSync.Daemon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Composition root (doc 14). Runs standalone; AppHost only orchestrates.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCloudSyncDaemon();

var host = builder.Build();
await host.RunAsync();

return host.Services.GetRequiredService<DaemonExit>().Code;
