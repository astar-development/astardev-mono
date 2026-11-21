using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

static string Project(string relative, [System.Runtime.CompilerServices.CallerFilePath] string file = "") =>
    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, relative));

builder.AddDotnetProject("CloudSync", Project("../../desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI/AStarDev.CloudSync.UI.csproj"))
    .WithEnvironment("ApplicationInsights__ConnectionString", "")
    .WithEnvironment("OpenTelemetry__OtlpEndpoint", builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] ?? "");

builder.Build().Run();
