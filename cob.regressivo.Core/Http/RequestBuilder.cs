using Cob.Regressivo.Core.Configuration;
using Cob.Regressivo.Core.Engine;
using Cob.Regressivo.Core.Templating;
using Flurl;
using Newtonsoft.Json.Linq;

namespace Cob.Regressivo.Core.Http;

public record BuiltRequest(
    string Method,
    string Url,
    Dictionary<string, string> Headers,
    string? Body,
    string? ContentType);

public static class RequestBuilder
{
    public static BuiltRequest Build(StepConfig step, PipelineExecutionContext context)
    {
        var ep  = step.Endpoint;
        var url = TemplateEngine.Render(ep.Url, context);

        foreach (var (k, v) in ep.QueryParams)
            url = url.SetQueryParam(k, TemplateEngine.Render(v, context));

        var headers = new Dictionary<string, string>();
        foreach (var (k, v) in context.Globals.DefaultHeaders)
            headers[k] = TemplateEngine.Render(v, context);
        foreach (var (k, v) in ep.Headers)
            headers[k] = TemplateEngine.Render(v, context);

        string? body        = null;
        string? contentType = null;

        if (ep.Body?.Content != null)
        {
            var rendered = TemplateEngine.Render(ep.Body.Content.ToString()!, context);

            switch (ep.Body.Type?.ToLower())
            {
                case "form":
                    contentType = "application/x-www-form-urlencoded";
                    try
                    {
                        var obj = JObject.Parse(rendered);
                        body = string.Join("&", obj.Properties()
                            .Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value.ToString())}"));
                    }
                    catch { body = rendered; }
                    break;

                case "xml":
                    contentType = "application/xml";
                    body = rendered;
                    break;

                default:
                    contentType = "application/json";
                    body = rendered;
                    break;
            }
        }

        return new BuiltRequest(ep.Method.ToUpper(), url, headers, body, contentType);
    }
}
