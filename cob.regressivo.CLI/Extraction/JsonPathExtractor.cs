using Newtonsoft.Json.Linq;

namespace Cob.Regressivo.CLI.Extraction;

public static class JsonPathExtractor
{
    public static Dictionary<string, string> Extract(string json, Dictionary<string, string> extractMap)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json) || extractMap.Count == 0) return result;

        JToken root;
        try { root = JToken.Parse(json); }
        catch { return result; }

        foreach (var (alias, path) in extractMap)
        {
            try
            {
                var token = root.SelectToken(path);
                result[alias] = token?.ToString() ?? string.Empty;
            }
            catch { result[alias] = string.Empty; }
        }

        return result;
    }
}
