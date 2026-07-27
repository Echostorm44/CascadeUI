using System;
using System.Collections.Generic;
using System.Linq;

namespace Cascade.IDE.Shared;

public sealed class SignalAnnotator
{
    public static IReadOnlyList<SignalAnnotation> Annotate(string sourceCode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        var annotations = new List<SignalAnnotation>();
        var lines = sourceCode.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("private ", StringComparison.Ordinal) &&
                !line.Contains("readonly ", StringComparison.Ordinal) &&
                !line.Contains('(', StringComparison.Ordinal) &&
                line.Contains(';', StringComparison.Ordinal))
            {
                var annotation = ParseFieldAnnotation(line, i + 1);
                if (annotation is not null)
                {
                    annotations.Add(annotation);
                }
            }

            if (line.StartsWith("private ", StringComparison.Ordinal) &&
                line.Contains("=>", StringComparison.Ordinal))
            {
                var annotation = ParseComputedAnnotation(line, i + 1);
                if (annotation is not null)
                {
                    annotations.Add(annotation);
                }
            }
        }

        return annotations;
    }

    public static IReadOnlyList<string> FindRenderDependencies(string sourceCode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        var info = ComponentAnalyzer.Analyze(sourceCode);
        if (info is null)
        {
            return [];
        }

        var reactiveFields = info.ReactiveFields
            .Where(field => field.IsReactive)
            .Select(field => field.Name)
            .ToList();

        var renderIdx = sourceCode.IndexOf("Render()", StringComparison.Ordinal);
        if (renderIdx < 0)
        {
            return [];
        }

        var braceStart = sourceCode.IndexOf('{', renderIdx);
        if (braceStart < 0)
        {
            var arrowIdx = sourceCode.IndexOf("=>", renderIdx, StringComparison.Ordinal);
            if (arrowIdx < 0)
            {
                return [];
            }

            var body = sourceCode[arrowIdx..];
            return reactiveFields.Where(field => ContainsWord(body, field)).ToList();
        }

        var depth = 1;
        var pos = braceStart + 1;
        while (pos < sourceCode.Length && depth > 0)
        {
            if (sourceCode[pos] == '{')
            {
                depth++;
            }
            else if (sourceCode[pos] == '}')
            {
                depth--;
            }

            pos++;
        }

        var renderBody = sourceCode[braceStart..pos];
        return reactiveFields.Where(field => ContainsWord(renderBody, field)).ToList();
    }

    private static bool ContainsWord(string text, string word)
    {
        var idx = 0;
        while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
        {
            var startOk = idx == 0 || (!char.IsLetterOrDigit(text[idx - 1]) && text[idx - 1] != '_');
            var endPos = idx + word.Length;
            var endOk = endPos >= text.Length || (!char.IsLetterOrDigit(text[endPos]) && text[endPos] != '_');
            if (startOk && endOk)
            {
                return true;
            }

            idx += word.Length;
        }

        return false;
    }

    private static SignalAnnotation? ParseFieldAnnotation(string line, int lineNumber)
    {
        var parts = line.TrimEnd(';').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        var type = parts[1];
        var name = parts[2].Split('=')[0].Trim();
        string? defaultValue = null;
        var eqIdx = line.IndexOf('=', StringComparison.Ordinal);
        if (eqIdx >= 0)
        {
            defaultValue = line[(eqIdx + 1)..].TrimEnd(';').Trim();
        }

        return new SignalAnnotation
        {
            FieldName = name,
            FieldType = type,
            LineNumber = lineNumber,
            Kind = SignalKind.ReactiveField,
            DefaultValue = defaultValue,
        };
    }

    private static SignalAnnotation? ParseComputedAnnotation(string line, int lineNumber)
    {
        var parts = line.Split("=>", 2);
        if (parts.Length < 2)
        {
            return null;
        }

        var declParts = parts[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (declParts.Length < 3)
        {
            return null;
        }

        var type = declParts[1];
        var name = declParts[2];
        var expression = parts[1].TrimEnd(';').Trim();

        return new SignalAnnotation
        {
            FieldName = name,
            FieldType = type,
            LineNumber = lineNumber,
            Kind = SignalKind.ComputedProperty,
            Expression = expression,
        };
    }
}

public sealed class SignalAnnotation
{
    public required string FieldName { get; init; }
    public required string FieldType { get; init; }
    public required int LineNumber { get; init; }
    public SignalKind Kind { get; init; }
    public string? DefaultValue { get; init; }
    public string? Expression { get; init; }
}

public enum SignalKind
{
    ReactiveField,
    ComputedProperty,
}
