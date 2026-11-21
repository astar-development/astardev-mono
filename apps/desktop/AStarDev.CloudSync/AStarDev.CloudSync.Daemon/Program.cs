using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.Daemon.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Composition root (doc 14). Runs standalone; AppHost only orchestrates.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<IpcOptions>().BindConfiguration(ApplicationConstants.IpcConfigurationSection);
builder.Services.AddHealthChecks(); // checks are added by the slices that own database, provider, queue and IPC
builder.Services.AddStartupStages();
builder.Services.AddHostedService<StartupHostedService>();

await builder.Build().RunAsync();
