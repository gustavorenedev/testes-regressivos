using Cob.Regressivo.Core.Engine;
using Scriban;
using Scriban.Runtime;

namespace Cob.Regressivo.Core.Templating;

public static class TemplateEngine
{
    public static string Render(string template, PipelineExecutionContext context)
    {
        var source = template.Replace("{{$", "{{");

        var model     = context.BuildTemplateModel();
        var scriptObj = ToScriptObject(model);
        scriptObj["correlationId"] = context.CorrelationId;
        scriptObj["timestamp"]     = context.StartedAt.ToString("yyyyMMddHHmmss");

        var templateCtx = new TemplateContext { StrictVariables = false };
        templateCtx.PushGlobal(scriptObj);

        var parsed = Template.Parse(source);
        if (parsed.HasErrors) return template;

        try
        {
            return parsed.Render(templateCtx);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Erro ao renderizar template '{source}': {ex.Message}", ex);
        }
    }

    private static ScriptObject ToScriptObject(Dictionary<string, object> dict)
    {
        var so = new ScriptObject();
        foreach (var (k, v) in dict)
            so[k] = ConvertValue(v);
        return so;
    }

    private static object ConvertValue(object value) => value switch
    {
        Dictionary<string, object> d => ToScriptObject(d),
        Dictionary<string, string> d => StringDictToScriptObject(d),
        _ => value,
    };

    private static ScriptObject StringDictToScriptObject(Dictionary<string, string> dict)
    {
        var so = new ScriptObject();
        foreach (var (k, v) in dict)
            so[k] = v;
        return so;
    }
}
