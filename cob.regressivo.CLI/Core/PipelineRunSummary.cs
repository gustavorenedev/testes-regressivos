using Cob.Regressivo.CLI.Configuration;

namespace Cob.Regressivo.CLI.Core;

public record PipelineRunSummary(
    string PipelineId,
    string PipelineName,
    string PipelineDescription,
    IReadOnlyList<string> Tags,
    string CorrelationId,
    DateTime StartedAt,
    TimeSpan TotalDuration,
    IReadOnlyList<ExecutionRecord> Records,
    ReportConfig Report)
{
    public int TotalSteps  => Records.Count;
    public int PassedSteps => Records.Count(r => r.Success);
    public int FailedSteps => Records.Count(r => !r.Success);

    public string OverallStatus => FailedSteps == 0 ? "SUCESSO"
        : PassedSteps == 0 ? "FALHA"
        : "FALHA PARCIAL";
}
