using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class LoggingArchitectureTestsShouldEnsure
{
    private static readonly string[] Production =
    [
        "AStarDev.CloudSync.Daemon",
        "AStarDev.CloudSync.Domain",
        "AStarDev.CloudSync.Errors",
        "AStarDev.CloudSync.FakeProvider",
        "AStarDev.CloudSync.IPC",
        "AStarDev.CloudSync.Observability",
        "AStarDev.CloudSync.OneDrive",
        "AStarDev.CloudSync.Persistence",
        "AStarDev.CloudSync.Ports",
        "AStarDev.CloudSync.SyncEngine",
        "AStarDev.CloudSync.UI",
        "AStarDev.SingleInstance"
    ];

    private static readonly Dictionary<short, OpCode> OpCodeTable = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    [Fact]
    public void have_no_direct_logger_calls_in_production_code() =>
        Production
            .Select(name => Assembly.Load(name))
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(Bodies)
            .Where(CallsLoggerExtensions)
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .ShouldBeEmpty();

    private static IEnumerable<MethodBase> Bodies(Type type)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        return type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags));
    }

    private static bool CallsLoggerExtensions(MethodBase method) =>
        method.GetMethodBody()?.GetILAsByteArray() is { } il && Callees(method, il).Any(callee => callee.DeclaringType == typeof(LoggerExtensions));

    private static IEnumerable<MethodBase> Callees(MethodBase method, byte[] il)
    {
        var typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var position = 0;
        while (position < il.Length)
        {
            var code = il[position] == 0xFE ? OpCodeTable[(short)(0xFE00 | il[position + 1])] : OpCodeTable[il[position]];
            position += code.Size;
            if (code == OpCodes.Call || code == OpCodes.Callvirt)
                yield return method.Module.ResolveMethod(BitConverter.ToInt32(il, position), typeArguments, methodArguments)!;
            position += OperandSize(code, il, position);
        }
    }

    private static int OperandSize(OpCode code, byte[] il, int position) => code.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
        _ => 4
    };
}
