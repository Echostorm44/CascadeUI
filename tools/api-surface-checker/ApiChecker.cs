using System.Reflection;
using System.Text;

namespace Cascade.Tools;

/// <summary>
/// Detects unintended public API surface additions in Cascade.UI.dll.
/// Compares the current public API against a baseline snapshot file.
///
/// Usage:
///   ApiChecker.exe --assembly path/to/Cascade.UI.dll --baseline api-baseline.txt
///   ApiChecker.exe --assembly path/to/Cascade.UI.dll --update-baseline api-baseline.txt
/// </summary>
public static class ApiChecker
{
    public static int Main(string[] args)
    {
        string? assemblyPath = null;
        string? baselinePath = null;
        bool updateBaseline = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--assembly" when i + 1 < args.Length:
                    assemblyPath = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--update-baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    updateBaseline = true;
                    break;
                case "--help":
                    PrintUsage();
                    return 0;
            }
        }

        if (assemblyPath is null || baselinePath is null)
        {
            PrintUsage();
            return 1;
        }

        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
            return 1;
        }

        var currentApi = ExtractPublicApi(assemblyPath);

        if (updateBaseline)
        {
            File.WriteAllText(baselinePath, currentApi, Encoding.UTF8);
            Console.WriteLine($"Baseline updated: {baselinePath}");
            Console.WriteLine($"  {CountLines(currentApi)} public API members");
            return 0;
        }

        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Baseline not found: {baselinePath}");
            Console.Error.WriteLine("Run with --update-baseline to create it.");
            return 1;
        }

        string baselineApi = File.ReadAllText(baselinePath, Encoding.UTF8);
        var (added, removed) = CompareApis(baselineApi, currentApi);

        if (added.Count == 0 && removed.Count == 0)
        {
            Console.WriteLine("Public API matches baseline.");
            return 0;
        }

        if (added.Count > 0)
        {
            Console.WriteLine($"ADDED ({added.Count} new public members):");
            foreach (string line in added)
            {
                Console.WriteLine($"  + {line}");
            }
        }

        if (removed.Count > 0)
        {
            Console.WriteLine($"REMOVED ({removed.Count} members missing from baseline):");
            foreach (string line in removed)
            {
                Console.WriteLine($"  - {line}");
            }
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("Public API has changed. If intentional, run with --update-baseline.");
        return 2;
    }

    /// <summary>
    /// Extracts the public API surface from an assembly as a sorted, deterministic
    /// text representation. Each line represents one public type or member.
    /// </summary>
    internal static string ExtractPublicApi(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var lines = new List<string>();

        foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            string typeKind = GetTypeKind(type);
            string baseName = type.BaseType is not null && type.BaseType != typeof(object)
                ? $" : {FormatTypeName(type.BaseType)}"
                : "";

            var interfaces = type.GetInterfaces()
                .Where(i => i.IsPublic)
                .OrderBy(i => i.FullName, StringComparer.Ordinal)
                .Select(FormatTypeName);

            string ifaceList = string.Join(", ", interfaces);
            if (!string.IsNullOrEmpty(ifaceList))
            {
                baseName += string.IsNullOrEmpty(baseName) ? $" : {ifaceList}" : $", {ifaceList}";
            }

            lines.Add($"{typeKind} {FormatTypeName(type)}{baseName}");

            // Public constructors
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(c => c.ToString(), StringComparer.Ordinal))
            {
                string paramList = FormatParameters(ctor.GetParameters());
                lines.Add($"  .ctor({paramList})");
            }

            // Public properties
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                string accessors = FormatPropertyAccessors(prop);
                string staticMod = prop.GetMethod?.IsStatic == true ? "static " : "";
                lines.Add($"  {staticMod}{FormatTypeName(prop.PropertyType)} {prop.Name} {{ {accessors} }}");
            }

            // Public methods (excluding property accessors, event accessors)
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ThenBy(m => m.GetParameters().Length))
            {
                string staticMod = method.IsStatic ? "static " : "";
                string paramList = FormatParameters(method.GetParameters());
                string genericSuffix = method.IsGenericMethod
                    ? $"<{string.Join(", ", method.GetGenericArguments().Select(FormatTypeName))}>"
                    : "";
                lines.Add($"  {staticMod}{FormatTypeName(method.ReturnType)} {method.Name}{genericSuffix}({paramList})");
            }

            // Public events
            foreach (var evt in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                string staticMod = evt.AddMethod?.IsStatic == true ? "static " : "";
                lines.Add($"  {staticMod}event {FormatTypeName(evt.EventHandlerType!)} {evt.Name}");
            }

            // Public fields (non-backing)
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(f => !f.Name.Contains('<', StringComparison.Ordinal))
                .OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                string staticMod = field.IsStatic ? "static " : "";
                string readonlyMod = field.IsInitOnly ? "readonly " : "";
                lines.Add($"  {staticMod}{readonlyMod}{FormatTypeName(field.FieldType)} {field.Name}");
            }
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    /// <summary>
    /// Compares two API snapshots and returns lists of added and removed lines.
    /// </summary>
    internal static (List<string> Added, List<string> Removed) CompareApis(string baseline, string current)
    {
        var baselineSet = new HashSet<string>(
            baseline.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        var currentSet = new HashSet<string>(
            current.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        var added = currentSet.Except(baselineSet).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var removed = baselineSet.Except(currentSet).OrderBy(s => s, StringComparer.Ordinal).ToList();

        return (added, removed);
    }

    private static string GetTypeKind(Type type)
    {
        if (type.IsEnum) { return "enum"; }
        if (type.IsInterface) { return "interface"; }
        if (type.IsValueType) { return "struct"; }
        if (type.IsAbstract && type.IsSealed) { return "static class"; }
        if (type.IsAbstract) { return "abstract class"; }
        if (type.IsSealed) { return "sealed class"; }
        return "class";
    }

    private static string FormatTypeName(Type type)
    {
        if (type == typeof(void)) { return "void"; }
        if (type == typeof(int)) { return "int"; }
        if (type == typeof(long)) { return "long"; }
        if (type == typeof(float)) { return "float"; }
        if (type == typeof(double)) { return "double"; }
        if (type == typeof(bool)) { return "bool"; }
        if (type == typeof(string)) { return "string"; }
        if (type == typeof(object)) { return "object"; }
        if (type == typeof(decimal)) { return "decimal"; }
        if (type == typeof(byte)) { return "byte"; }
        if (type == typeof(char)) { return "char"; }
        if (type == typeof(nint)) { return "nint"; }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return $"{FormatTypeName(underlying)}?";
        }

        if (type.IsGenericType)
        {
            string name = type.Name;
            int backtick = name.IndexOf('`', StringComparison.Ordinal);
            if (backtick >= 0)
            {
                name = name[..backtick];
            }
            string args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{name}<{args}>";
        }

        return type.Name;
    }

    private static string FormatParameters(ParameterInfo[] parameters)
    {
        return string.Join(", ", parameters.Select(p =>
        {
            string modifier = "";
            if (p.IsOut) { modifier = "out "; }
            else if (p.ParameterType.IsByRef) { modifier = "ref "; }
            return $"{modifier}{FormatTypeName(p.ParameterType)} {p.Name}";
        }));
    }

    private static string FormatPropertyAccessors(PropertyInfo prop)
    {
        var parts = new List<string>();
        if (prop.GetMethod?.IsPublic == true) { parts.Add("get;"); }
        if (prop.SetMethod?.IsPublic == true)
        {
            parts.Add(prop.SetMethod.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(t => t.FullName == "System.Runtime.CompilerServices.IsExternalInit")
                ? "init;" : "set;");
        }
        return string.Join(" ", parts);
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text)) { return 0; }
        int count = 0;
        foreach (char c in text)
        {
            if (c == '\n') { count++; }
        }
        return count;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Cascade API Surface Checker");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ApiChecker --assembly <path> --baseline <path>           Compare against baseline");
        Console.WriteLine("  ApiChecker --assembly <path> --update-baseline <path>    Create/update baseline");
        Console.WriteLine("  ApiChecker --help                                        Show this help");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  API matches baseline (or help/update)");
        Console.WriteLine("  1  Missing arguments or file not found");
        Console.WriteLine("  2  API has changed (additions or removals)");
    }
}
