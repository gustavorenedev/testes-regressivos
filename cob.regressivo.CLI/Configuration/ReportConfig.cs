namespace Cob.Regressivo.CLI.Configuration;

public class ReportConfig
{
    public string Title { get; set; } = "Pipeline Execution Report";
    public string OutputPath { get; set; } = "./reports/report-{{correlationId}}.pdf";
    public bool IncludeRequestBody { get; set; } = true;
    public bool IncludeResponseBody { get; set; } = true;
    public List<string> SensitiveFields { get; set; } = [];
}
