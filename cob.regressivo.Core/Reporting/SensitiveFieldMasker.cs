using System.Text.RegularExpressions;

namespace Cob.Regressivo.Core.Reporting;

public static class SensitiveFieldMasker
{
    private const string Mask = "***";

    public static string MaskHeaders(Dictionary<string, string> headers, IEnumerable<string> sensitiveFields)
    {
        var masked = new Dictionary<string, string>(headers);
        foreach (var field in sensitiveFields)
        {
            var key = masked.Keys.FirstOrDefault(k => k.Equals(field, StringComparison.OrdinalIgnoreCase));
            if (key != null) masked[key] = Mask;
        }
        return string.Join("\n", masked.Select(kv => $"{kv.Key}: {kv.Value}"));
    }

    public static string? MaskBody(string? body, IEnumerable<string> sensitiveFields)
    {
        if (body == null) return null;
        return sensitiveFields.Aggregate(body, (current, field) =>
            Regex.Replace(current,
                $@"""({Regex.Escape(field)})""\s*:\s*""[^""]*""",
                $@"""{field}"": ""{Mask}""",
                RegexOptions.IgnoreCase));
    }
}
