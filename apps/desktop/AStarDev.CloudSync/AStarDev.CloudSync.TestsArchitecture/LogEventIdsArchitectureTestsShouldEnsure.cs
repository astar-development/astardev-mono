using System.Reflection;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class LogEventIdsArchitectureTestsShouldEnsure
{
    private static readonly Dictionary<string, int> Blocks = new()
    {
        ["AStarDev.CloudSync.SyncEngine"] = 1000,
        ["AStarDev.CloudSync.IPC"] = 2000,
        ["AStarDev.CloudSync.OneDrive"] = 3000,
        ["AStarDev.CloudSync.Persistence"] = 4000,
        ["AStarDev.CloudSync.Observability"] = 5000,
        ["AStarDev.CloudSync.Daemon"] = 6000,
        ["AStarDev.CloudSync.UI"] = 7000,
        ["AStarDev.SingleInstance"] = 8000,
        ["AStarDev.CloudSync.Classification"] = 9000
    };

    private const int BlockSize = 1000;

    [Fact]
    public void be_unique_across_the_solution() =>
        Declared()
            .GroupBy(message => message.EventId)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(message => message.Name))}")
            .ShouldBeEmpty();

    [Fact]
    public void fall_within_the_block_of_their_module() =>
        Declared()
            .Where(message => message.EventId < Blocks[message.Module] || message.EventId >= Blocks[message.Module] + BlockSize)
            .Select(message => $"{message.Name} uses {message.EventId}, block starts at {Blocks[message.Module]}")
            .ShouldBeEmpty();

    [Fact]
    public void give_the_ui_log_the_first_id_of_the_ui_block() =>
        Declared()
            .Single(message => message.Name == "UiLog.ThemeFailed")
            .EventId.ShouldBe(7000);

    private static IEnumerable<DeclaredMessage> Declared() =>
        Blocks.Keys
            .Select(name => Assembly.Load(name))
            .SelectMany(assembly => assembly.GetTypes().SelectMany(type => type
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(method => (type, method, attribute: method.GetCustomAttribute<LoggerMessageAttribute>()))
                .Where(found => found.attribute is not null)
                .Select(found => new DeclaredMessage(assembly.GetName().Name!, $"{found.type.Name}.{found.method.Name}", found.attribute!.EventId))));

    private sealed record DeclaredMessage(string Module, string Name, int EventId);
}
