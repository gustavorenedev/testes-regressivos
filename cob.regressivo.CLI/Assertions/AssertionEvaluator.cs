using Cob.Regressivo.CLI.Configuration;
using Cob.Regressivo.CLI.Core;
using Newtonsoft.Json.Linq;

namespace Cob.Regressivo.CLI.Assertions;

public static class AssertionEvaluator
{
    public static List<AssertionResult> Evaluate(
        int statusCode, string responseBody, List<AssertionConfig> assertions)
    {
        var results = new List<AssertionResult>();
        JToken? root = null;

        if (!string.IsNullOrWhiteSpace(responseBody))
            try { root = JToken.Parse(responseBody); } catch { }

        foreach (var a in assertions)
        {
            if (a.StatusCode.HasValue)
            {
                var ok = statusCode == a.StatusCode.Value;
                results.Add(new AssertionResult(ok,
                    $"StatusCode {statusCode} {(ok ? "==" : "!=")} {a.StatusCode.Value}"));
                continue;
            }

            if (a.Path != null)
            {
                var token = root?.SelectToken(a.Path);
                var val = token?.ToString();

                var (passed, msg) = a.Operator switch
                {
                    "notEmpty"  => (!string.IsNullOrEmpty(val),
                                   $"{a.Path} é {(string.IsNullOrEmpty(val) ? "vazio" : "não-vazio")}"),
                    "equals"    => (val == a.Value,
                                   $"{a.Path} [{val}] {(val == a.Value ? "==" : "!=")} [{a.Value}]"),
                    "contains"  => (val?.Contains(a.Value ?? "") == true,
                                   $"{a.Path} {(val?.Contains(a.Value ?? "") == true ? "contém" : "não contém")} [{a.Value}]"),
                    "notNull"   => (token != null && token.Type != JTokenType.Null,
                                   $"{a.Path} é {(token == null ? "null" : "não-null")}"),
                    _           => (false, $"Operador desconhecido: {a.Operator}")
                };

                results.Add(new AssertionResult(passed, msg));
            }
        }

        return results;
    }
}
