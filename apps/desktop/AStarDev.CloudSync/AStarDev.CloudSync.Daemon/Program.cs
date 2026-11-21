using AStarDev.CloudSync.Daemon;
using Microsoft.Extensions.Hosting;

// Composition root (doc 14). Runs standalone; AppHost only orchestrates.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCloudSyncDaemon();

await builder.Build().RunAsync();
