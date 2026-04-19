namespace Cob.Regressivo.CLI.Configuration;

public class PipelineConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string OnError { get; set; } = "stop";
    public List<StepConfig> Steps { get; set; } = [];
    public Dictionary<string, string> Output { get; set; } = new();
    public ReportConfig Report { get; set; } = new();
}
