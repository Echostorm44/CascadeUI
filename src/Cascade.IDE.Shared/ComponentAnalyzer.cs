using System;
using System.Collections.Generic;
using System.Linq;

namespace Cascade.IDE.Shared;

public sealed class ComponentAnalyzer
{
    public static ComponentInfo? Analyze(string sourceCode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        var className = ExtractClassName(sourceCode);
        if (className is null)
        {
            return null;
        }

        var baseTypes = ExtractBaseTypes(sourceCode);
        var baseClass = baseTypes.Count > 0 ? baseTypes[0] : null;
        var isComponent = baseTypes.Any(type =>
            type.Contains("Component", StringComparison.Ordinal) ||
            type.Contains("Node", StringComparison.Ordinal));

        return new ComponentInfo
        {
            ClassName = className,
            BaseClass = baseClass ?? "object",
            IsComponent = isComponent,
            ReactiveFields = ExtractReactiveFields(sourceCode),
            RenderMethods = ExtractRenderMethods(sourceCode),
            HasRenderMethod = sourceCode.Contains("Render()", StringComparison.Ordinal),
            SourceLength = sourceCode.Length,
        };
    }

    public static IReadOnlyList<string> FindComponents(string sourceCode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        var results = new List<string>();
        var idx = 0;

        while ((idx = sourceCode.IndexOf("class ", idx, StringComparison.Ordinal)) >= 0)
        {
            var nameStart = idx + 6;
            var nameEnd = sourceCode.IndexOfAny([' ', ':', '{', '\n', '\r', '<'], nameStart);
            if (nameEnd > nameStart)
            {
                var name = sourceCode[nameStart..nameEnd].Trim();
                if (name.Length > 0 && char.IsUpper(name[0]))
                {
                    var colonIdx = sourceCode.IndexOf(':', nameEnd);
                    if (colonIdx >= 0 && colonIdx < sourceCode.IndexOf('{', nameEnd))
                    {
                        var braceIdx = sourceCode.IndexOf('{', colonIdx);
                        if (braceIdx > colonIdx)
                        {
                            var bases = sourceCode[(colonIdx + 1)..braceIdx];
                            if (bases.Contains("Component", StringComparison.Ordinal))
                            {
                                results.Add(name);
                            }
                        }
                    }
                }
            }

            idx = nameStart;
        }

        return results;
    }

    private static string? ExtractClassName(string source)
    {
        var idx = source.IndexOf("class ", StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var nameStart = idx + 6;
        var nameEnd = source.IndexOfAny([' ', ':', '{', '\n', '\r', '<'], nameStart);
        if (nameEnd <= nameStart)
        {
            return null;
        }

        return source[nameStart..nameEnd].Trim();
    }

    private static List<string> ExtractBaseTypes(string source)
    {
        var classIdx = source.IndexOf("class ", StringComparison.Ordinal);
        if (classIdx < 0)
        {
            return [];
        }

        var colonIdx = source.IndexOf(':', classIdx);
        if (colonIdx < 0)
        {
            return [];
        }

        var endIdx = source.IndexOfAny(['{', '\n', '\r'], colonIdx + 1);
        if (endIdx < 0)
        {
            endIdx = source.Length;
        }

        var baseList = source[(colonIdx + 1)..endIdx];
        return baseList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToList();
    }

    private static string? ExtractBaseClass(string source)
    {
        var types = ExtractBaseTypes(source);
        return types.Count > 0 ? types[0] : null;
    }

    private static IReadOnlyList<FieldInfo> ExtractReactiveFields(string source)
    {
        var fields = new List<FieldInfo>();
        var idx = 0;

        while ((idx = source.IndexOf("private ", idx, StringComparison.Ordinal)) >= 0)
        {
            var lineEnd = source.IndexOf('\n', idx);
            if (lineEnd < 0)
            {
                lineEnd = source.Length;
            }

            var line = source[idx..lineEnd].Trim();
            if (!line.Contains('(', StringComparison.Ordinal) && line.Contains(';', StringComparison.Ordinal))
            {
                var isReadonly = line.Contains("readonly ", StringComparison.Ordinal);
                var parts = line.TrimEnd(';').Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var type = isReadonly ? parts[2] : parts[1];
                    var namePartIndex = isReadonly && parts.Length >= 4 ? 3 : 2;
                    var name = parts[namePartIndex].TrimEnd(';').Split('=')[0].Trim();
                    fields.Add(new FieldInfo
                    {
                        Name = name,
                        Type = type,
                        IsReactive = !isReadonly,
                        IsReadonly = isReadonly,
                    });
                }
            }

            idx += 8;
        }

        return fields;
    }

    private static IReadOnlyList<string> ExtractRenderMethods(string source)
    {
        var methods = new List<string>();
        var idx = 0;

        while ((idx = source.IndexOf("Node ", idx, StringComparison.Ordinal)) >= 0)
        {
            var parenIdx = source.IndexOf('(', idx);
            if (parenIdx > idx)
            {
                var methodName = source[(idx + 5)..parenIdx].Trim();
                if (methodName.Length > 0 && char.IsUpper(methodName[0]) && !methodName.Contains(' ', StringComparison.Ordinal))
                {
                    methods.Add(methodName);
                }
            }

            idx += 5;
        }

        return methods;
    }
}

public sealed class ComponentInfo
{
    public required string ClassName { get; init; }
    public required string BaseClass { get; init; }
    public bool IsComponent { get; init; }
    public IReadOnlyList<FieldInfo> ReactiveFields { get; init; } = [];
    public IReadOnlyList<string> RenderMethods { get; init; } = [];
    public bool HasRenderMethod { get; init; }
    public int SourceLength { get; init; }
}

public sealed class FieldInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsReactive { get; init; }
    public bool IsReadonly { get; init; }
}
