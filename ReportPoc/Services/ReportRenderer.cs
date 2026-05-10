namespace ReportPoc.Services;

public interface IReportRenderer
{
    string Render(string templateText, Dictionary<string, object?> dataModel, Dictionary<string, object?> computed);
}

public sealed class ReportHtmlRenderer : IReportRenderer
{
    public string Render(string templateText, Dictionary<string, object?> dataModel, Dictionary<string, object?> computed)
    {
        var rootModel = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in dataModel)
        {
            rootModel[pair.Key] = pair.Value;
        }
        rootModel["computed"] = computed;

        var withLoops = RenderLoops(templateText, rootModel);
        var rendered = ReplacePlaceholders(withLoops, rootModel, null);
        return rendered;
    }

    private static string RenderLoops(string template, Dictionary<string, object?> root)
    {
        var loopRegex = new System.Text.RegularExpressions.Regex(
            @"\{\{#each\s+([a-zA-Z_][a-zA-Z0-9_]*)\}\}(.*?)\{\{/each\}\}",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        var result = loopRegex.Replace(template, match =>
        {
            var collectionName = match.Groups[1].Value;
            var block = match.Groups[2].Value;

            if (!root.TryGetValue(collectionName, out var value) || value is not IEnumerable<object> generic)
            {
                if (value is System.Collections.IEnumerable enumerable && value is not string)
                {
                    var fallbackText = string.Empty;
                    foreach (var item in enumerable)
                    {
                        if (item is Dictionary<string, object?> map)
                        {
                            fallbackText += ReplacePlaceholders(block, root, map);
                        }
                    }
                    return fallbackText;
                }

                return string.Empty;
            }

            var renderedRows = string.Empty;
            foreach (var item in generic)
            {
                if (item is Dictionary<string, object?> itemMap)
                {
                    renderedRows += ReplacePlaceholders(block, root, itemMap);
                }
            }

            return renderedRows;
        });

        return result;
    }

    private static string ReplacePlaceholders(string text, Dictionary<string, object?> root, Dictionary<string, object?>? current)
    {
        var tokenRegex = new System.Text.RegularExpressions.Regex(@"\{\{([^{}]+)\}\}");
        return tokenRegex.Replace(text, match =>
        {
            var token = match.Groups[1].Value.Trim();
            if (token.StartsWith("#", StringComparison.OrdinalIgnoreCase) || token.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (TryResolvePath(root, current, token, out var value))
            {
                return value?.ToString() ?? string.Empty;
            }

            return string.Empty;
        });
    }

    private static bool TryResolvePath(
        Dictionary<string, object?> root,
        Dictionary<string, object?>? current,
        string path,
        out object? value)
    {
        value = null;
        if (TryGetFromMap(current, path, out value))
        {
            return true;
        }

        if (TryGetFromMap(root, path, out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetFromMap(Dictionary<string, object?>? map, string path, out object? value)
    {
        value = null;
        if (map is null)
        {
            return false;
        }

        if (!path.Contains('.'))
        {
            return map.TryGetValue(path, out value);
        }

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        object? current = map;
        foreach (var part in parts)
        {
            if (current is Dictionary<string, object?> typed)
            {
                if (!typed.TryGetValue(part, out current))
                {
                    return false;
                }
                continue;
            }

            return false;
        }

        value = current;
        return true;
    }
}
