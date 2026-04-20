namespace Cob.Regressivo.Core.Configuration;

public class EndpointConfig
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string> QueryParams { get; set; } = new();
    public BodyConfig? Body { get; set; }
}
