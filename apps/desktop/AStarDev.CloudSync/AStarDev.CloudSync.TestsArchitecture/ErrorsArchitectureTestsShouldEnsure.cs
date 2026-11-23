using System.Reflection;
using System.Runtime.CompilerServices;
using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class ErrorsArchitectureTestsShouldEnsure
{
    private static readonly string[] Modules =
    [
        "AStarDev.CloudSync.Daemon",
        "AStarDev.CloudSync.IPC",
        "AStarDev.CloudSync.OneDrive",
        "AStarDev.CloudSync.Persistence",
        "AStarDev.CloudSync.Ports",
        "AStarDev.CloudSync.SyncEngine",
        "AStarDev.CloudSync.UI"
    ];

    [Fact]
    public void find_the_error_unions_and_custom_exceptions()
    {
        UnionTypes().Count().ShouldBeGreaterThanOrEqualTo(11);
        ExceptionTypes().ShouldNotBeEmpty();
    }

    [Fact]
    public void give_every_error_case_a_code_and_link() =>
        Facts()
            .Where(found => !IsComplete(found))
            .Select(found => found.Type.FullName!)
            .ShouldBeEmpty();

    [Fact]
    public void use_each_code_once() =>
        CaseFacts()
            .Where(found => found.Code.Length > 0)
            .GroupBy(found => found.Code)
            .Where(group => group.Select(found => found.Type).Distinct().Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(found => found.Type.FullName))}")
            .ShouldBeEmpty();

    private static bool IsComplete(ErrorFacts found) =>
        ErrorCode.Create(found.Code).Value is Ok<ErrorCode> && found.Link == ErrorLinks.Root + found.Code;

    private static IEnumerable<ErrorFacts> Facts() => CaseFacts().Concat(ExceptionTypes().Select(FromException));

    private static IEnumerable<ErrorFacts> CaseFacts() =>
        UnionTypes().SelectMany(CaseTypes).Concat(AppTypes().Where(type => type.IsClass && typeof(IErrorInfo).IsAssignableFrom(type))).Distinct().Select(FromCase);

    private static ErrorFacts FromCase(Type type)
    {
        if (!typeof(IErrorInfo).IsAssignableFrom(type)) return new ErrorFacts(type, string.Empty, string.Empty);
        var info = (IErrorInfo)RuntimeHelpers.GetUninitializedObject(type);

        return new ErrorFacts(type, info.Code.Value, info.HelpLink);
    }

    private static ErrorFacts FromException(Type type)
    {
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(candidate => candidate.GetParameters().Length)
            .First();
        var arguments = constructor.GetParameters().Select(parameter => Placeholder(parameter.ParameterType)).ToArray();
        var exception = (Exception)constructor.Invoke(arguments);
        var link = exception.HelpLink ?? string.Empty;

        return new ErrorFacts(type, link.StartsWith(ErrorLinks.Root, StringComparison.Ordinal) ? link[ErrorLinks.Root.Length..] : string.Empty, link);
    }

    private static object? Placeholder(Type type) =>
        type == typeof(string) ? string.Empty
        : type.IsValueType ? Activator.CreateInstance(type)
        : type.IsAbstract || type.IsInterface ? null
        : RuntimeHelpers.GetUninitializedObject(type);

    private static IEnumerable<Type> UnionTypes() =>
        AppTypes().Where(type => type.Name.EndsWith("Error", StringComparison.Ordinal) && type.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "UnionAttribute"));

    private static IEnumerable<Type> CaseTypes(Type union) =>
        union.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(constructor => constructor.GetParameters().Length == 1)
            .Select(constructor => constructor.GetParameters()[0].ParameterType);

    private static IEnumerable<Type> ExceptionTypes() =>
        AppTypes().Where(type => type.IsSubclassOf(typeof(Exception)));

    private static IEnumerable<Type> AppTypes() =>
        Modules.Select(name => Assembly.Load(name)).SelectMany(assembly => assembly.GetTypes());

    private sealed record ErrorFacts(Type Type, string Code, string Link);
}
