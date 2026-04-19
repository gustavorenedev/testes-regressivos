using Cob.Regressivo.CLI.Configuration;

namespace Cob.Regressivo.CLI.Core;

public class PipelineExecutionContext
{
    public string CorrelationId { get; }
    public DateTime StartedAt { get; }
    public GlobalsConfig Globals { get; }

    private readonly Dictionary<string, string> _variables;
    private readonly Dictionary<string, Dictionary<string, string>> _stepExtractions = new();

    public PipelineExecutionContext(string correlationId, Dictionary<string, string> variables, GlobalsConfig globals)
    {
        CorrelationId = correlationId;
        StartedAt = DateTime.UtcNow;
        Globals = globals;
        _variables = ResolveEnvVariables(variables);
    }

    public void AddExtractions(string stepId, Dictionary<string, string> extractions)
        => _stepExtractions[NormalizeId(stepId)] = extractions;

    public Dictionary<string, object> BuildTemplateModel()
    {
        var stepsObj = _stepExtractions
            .ToDictionary(kv => kv.Key, kv => (object)kv.Value);

        return new Dictionary<string, object>
        {
            ["globals"] = new Dictionary<string, string> { ["baseUrl"] = Globals.BaseUrl },
            ["variables"] = new Dictionary<string, string>(_variables),
            ["steps"] = stepsObj,
        };
    }

    internal static string NormalizeId(string id) => id.Replace("-", "_");

    private static Dictionary<string, string> ResolveEnvVariables(Dictionary<string, string> variables)
    {
        var result = new Dictionary<string, string>(variables);
        foreach (var key in result.Keys.ToList())
        {
            var val = result[key];
            if (val.StartsWith("${ENV:") && val.EndsWith("}"))
                result[key] = Environment.GetEnvironmentVariable(val[6..^1]) ?? string.Empty;
        }
        return result;
    }
}
