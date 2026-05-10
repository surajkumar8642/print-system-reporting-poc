using System.Text.RegularExpressions;

namespace ReportPoc.Services;

public static class FormulaFieldPathExtractor
{
    private static readonly Regex FieldPathRegex = new(@"\b[a-zA-Z_][a-zA-Z0-9_]*\.[a-zA-Z0-9_]+\b", RegexOptions.Compiled);

    public static IEnumerable<string> ExtractPaths(string expression)
    {
        return FieldPathRegex.Matches(expression)
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
