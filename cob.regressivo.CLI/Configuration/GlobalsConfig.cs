namespace Cob.Regressivo.CLI.Configuration;

public class GlobalsConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
    public RetryPolicyConfig RetryPolicy { get; set; } = new();
}
