using AStarDev.CloudSync.Daemon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Composition root (doc 14). Runs standalone; AppHost only orchestrates.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCloudSyncDaemon();

var host = builder.Build();
var exit = host.Services.GetRequiredService<DaemonExit>();
await host.RunAsync();

return exit.Code;
