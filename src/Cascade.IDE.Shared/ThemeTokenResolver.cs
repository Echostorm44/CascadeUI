using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Cascade.IDE.Shared;

public sealed class ThemeTokenResolver
{
    private readonly ConcurrentDictionary<string, ThemeToken> tokens = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ThemeToken> Tokens => tokens;

    public void Register(string path, ThemeToken token)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(token);
        tokens[path] = token;
    }

    public ThemeToken? Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return tokens.TryGetValue(path, out var token) ? token : null;
    }

    public IReadOnlyList<ThemeToken> FindByCategory(string category)
    {
        ArgumentException.ThrowIfNullOrEmpty(category);
        var prefix = category + ".";
        return tokens
            .Where(entry => entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Value)
            .ToList();
    }

    public void LoadDefaults(string themeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(themeName);
        Register($"{themeName}.Colors.Primary", new ThemeToken
        {
            Path = $"{themeName}.Colors.Primary",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = themeName.Contains("Apple", StringComparison.OrdinalIgnoreCase) ? "#0A84FF" : "#6750A4",
        });
        Register($"{themeName}.Colors.Background", new ThemeToken
        {
            Path = $"{themeName}.Colors.Background",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = "#FFFFFF",
        });
        Register($"{themeName}.Colors.Surface", new ThemeToken
        {
            Path = $"{themeName}.Colors.Surface",
            Category = "Colors",
            ValueType = TokenValueType.Color,
            Value = "#F5F5F5",
        });
        Register($"{themeName}.Spacing.Base", new ThemeToken
        {
            Path = $"{themeName}.Spacing.Base",
            Category = "Spacing",
            ValueType = TokenValueType.Number,
            Value = "8",
        });
        Register($"{themeName}.Radius.Base", new ThemeToken
        {
            Path = $"{themeName}.Radius.Base",
            Category = "Radius",
            ValueType = TokenValueType.Number,
            Value = themeName.Contains("Apple", StringComparison.OrdinalIgnoreCase) ? "8" : "12",
        });
        Register($"{themeName}.Typography.FontFamily", new ThemeToken
        {
            Path = $"{themeName}.Typography.FontFamily",
            Category = "Typography",
            ValueType = TokenValueType.String,
            Value = themeName.Contains("Apple", StringComparison.OrdinalIgnoreCase) ? "SF Pro" : "Segoe UI Variable",
        });
    }

    public IReadOnlyList<TokenDiff> DiffAgainst(ThemeTokenResolver baseResolver)
    {
        ArgumentNullException.ThrowIfNull(baseResolver);
        var diffs = new List<TokenDiff>();

        foreach (var (path, token) in tokens)
        {
            var baseToken = baseResolver.Resolve(path);
            if (baseToken is null)
            {
                diffs.Add(new TokenDiff { Path = path, Kind = DiffKind.Added, NewValue = token.Value });
            }
            else if (!string.Equals(baseToken.Value, token.Value, StringComparison.Ordinal))
            {
                diffs.Add(new TokenDiff { Path = path, Kind = DiffKind.Changed, OldValue = baseToken.Value, NewValue = token.Value });
            }
        }

        foreach (var (path, _) in baseResolver.Tokens)
        {
            if (!tokens.ContainsKey(path))
            {
                var oldValue = baseResolver.Resolve(path)?.Value;
                diffs.Add(new TokenDiff { Path = path, Kind = DiffKind.Removed, OldValue = oldValue });
            }
        }

        return diffs;
    }
}

public sealed class ThemeToken
{
    public required string Path { get; init; }
    public required string Category { get; init; }
    public required TokenValueType ValueType { get; init; }
    public required string Value { get; init; }
    public string? Description { get; init; }
}

public enum TokenValueType
{
    Color,
    Number,
    String,
    Enum,
}

public sealed class TokenDiff
{
    public required string Path { get; init; }
    public DiffKind Kind { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public enum DiffKind
{
    Added,
    Changed,
    Removed,
}
