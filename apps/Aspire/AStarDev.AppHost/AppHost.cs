using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

static string Project(string relative, [System.Runtime.CompilerServices.CallerFilePath] string file = "") =>
    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, relative));

var socketPath = Path.Combine(
    Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtimeDir
        ? runtimeDir
        : Path.Combine(Path.GetTempPath(), Environment.UserName),
    "astardev-cloudsync",
    "daemon.sock");

var otlpEndpoint = builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] ?? "";

var daemon = builder.AddDotnetProject("CloudSyncDaemon", Project("../../desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon/AStarDev.CloudSync.Daemon.csproj"))
    .WithEnvironment("Ipc__SocketPath", socketPath)
    .WithEnvironment("OpenTelemetry__OtlpEndpoint", otlpEndpoint);

builder.AddDotnetProject("CloudSync", Project("../../desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI/AStarDev.CloudSync.UI.csproj"))
    .WithEnvironment("Ipc__SocketPath", socketPath)
    .WithEnvironment("ApplicationInsights__ConnectionString", "")
    .WithEnvironment("OpenTelemetry__OtlpEndpoint", otlpEndpoint)
    .WaitFor(daemon);

builder.Build().Run();
