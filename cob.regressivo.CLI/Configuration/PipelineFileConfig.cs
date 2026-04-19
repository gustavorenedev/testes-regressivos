namespace Cob.Regressivo.CLI.Configuration;

public class PipelineFileConfig
{
    public string Version { get; set; } = "1.0";
    public GlobalsConfig Globals { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<PipelineConfig> Pipelines { get; set; } = [];
}
