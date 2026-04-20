namespace Cob.Regressivo.Core.Configuration;

public class StepConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> DependsOn { get; set; } = [];
    public EndpointConfig Endpoint { get; set; } = new();
    public Dictionary<string, string> Extract { get; set; } = new();
    public List<AssertionConfig> Assertions { get; set; } = [];
    public RetryPolicyConfig? RetryPolicy { get; set; }
}
