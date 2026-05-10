using System.Globalization;
using System.Text.RegularExpressions;

namespace ReportPoc.Services;

public sealed class ReportFormulaEvaluator
{
    public Dictionary<string, object?> BuildFormulaContext(Dictionary<string, object?> dataModel)
    {
        return new Dictionary<string, object?>(dataModel, StringComparer.OrdinalIgnoreCase);
    }

    public decimal Evaluate(string expression, Dictionary<string, object?> dataModel, Dictionary<string, object?> computedModel)
    {
        var safeContext = new Dictionary<string, object?>(dataModel, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in computedModel)
        {
            safeContext[$"computed.{pair.Key}"] = pair.Value;
        }

        var parser = new FormulaTermReader(expression);
        decimal accumulator = 0m;
        var op = 1m;

        while (parser.TryReadTerm(out var token))
        {
            var term = token.Trim();
            if (string.Equals(term, "+", StringComparison.OrdinalIgnoreCase))
            {
                op = 1m;
                continue;
            }

            if (string.Equals(term, "-", StringComparison.OrdinalIgnoreCase))
            {
                op = -1m;
                continue;
            }

            var value = EvaluateTerm(term, safeContext, op);
            accumulator += value;
        }

        return accumulator;
    }

    private static decimal EvaluateTerm(string term, Dictionary<string, object?> context, decimal sign)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return 0m;
        }

        var aggMatch = Regex.Match(term, @"^(?<fn>[a-zA-Z_][a-zA-Z0-9_]*)\((?<dataset>[^.]+)\.(?<field>[^)]+)\)$", RegexOptions.IgnoreCase);
        if (aggMatch.Success)
        {
            var functionName = aggMatch.Groups["fn"].Value;
            var dataset = aggMatch.Groups["dataset"].Value;
            var field = aggMatch.Groups["field"].Value;
            if (!context.TryGetValue(dataset, out var datasetValue) || datasetValue is not IEnumerable<object> items)
            {
                if (datasetValue is System.Collections.IEnumerable enumerable)
                {
                    return enumerable.Cast<object?>().Sum(item => ToDecimal(ResolveValue(item, field, null)));
                }

                return 0m;
            }

            var typed = items.Cast<object?>().ToList();
            return functionName.Equals("sum", StringComparison.OrdinalIgnoreCase)
                ? typed.Sum(item => ToDecimal(ResolveValue(item, field, null))) * sign
                : functionName.Equals("count", StringComparison.OrdinalIgnoreCase)
                    ? typed.Count(item => ResolveValue(item, field, null) is not null) * sign
                    : 0m;
        }

        if (decimal.TryParse(term, NumberStyles.Number, CultureInfo.InvariantCulture, out var direct))
        {
            return direct * sign;
        }

        if (term.Contains('.'))
        {
            return ToDecimal(ResolveByPath(context, term, null)) * sign;
        }

        if (!context.TryGetValue(term, out var directValue))
        {
            return 0m;
        }

        return ToDecimal(directValue) * sign;
    }

    private static object? ResolveByPath(Dictionary<string, object?> context, string path, object? fallback)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return fallback;
        }

        object? current = context;
        foreach (var segment in segments)
        {
            if (current is Dictionary<string, object?> map)
            {
                if (!map.TryGetValue(segment, out current))
                {
                    return fallback;
                }
                continue;
            }

            return fallback;
        }

        return current;
    }

    private static object? ResolveValue(object? input, string field, object? fallback)
    {
        if (input is Dictionary<string, object?> map)
        {
            return map.TryGetValue(field, out var value) ? value : fallback;
        }

        return fallback;
    }

    private static decimal ToDecimal(object? value)
    {
        return value switch
        {
            null => 0m,
            decimal d => d,
            double d => (decimal)d,
            float f => (decimal)f,
            int i => i,
            long i => i,
            string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0m
        };
    }

    private sealed class FormulaTermReader
    {
        private readonly string _expression;
        private int _index;

        public FormulaTermReader(string expression)
        {
            _expression = expression ?? string.Empty;
        }

        public bool TryReadTerm(out string term)
        {
            while (_index < _expression.Length && char.IsWhiteSpace(_expression[_index]))
            {
                _index++;
            }

            if (_index >= _expression.Length)
            {
                term = string.Empty;
                return false;
            }

            if (_expression[_index] == '+' || _expression[_index] == '-')
            {
                term = _expression[_index].ToString();
                _index++;
                return true;
            }

            var start = _index;
            while (_index < _expression.Length && _expression[_index] is not '+' and not '-')
            {
                _index++;
            }

            term = _expression[start.._expression.Length];
            if (_index <= _expression.Length - 1 && (_expression[_index] == '+' || _expression[_index] == '-'))
            {
                var value = _expression[start.._index];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    term = value;
                }
            }

            term = term.Trim();
            return true;
        }
    }
}

public static class JsonElementUtilities
{
    public static object? ToClrObject(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString(),
            System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDecimal(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
