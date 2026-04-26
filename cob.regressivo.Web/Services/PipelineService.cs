using Cob.Regressivo.CLI.Configuration;
using Cob.Regressivo.CLI.Core;
using Cob.Regressivo.CLI.Reporting;
using Newtonsoft.Json;

namespace Cob.Regressivo.Web.Services;

public class PipelineService
{
    private readonly List<(PipelineConfig Pipeline, PipelineFileConfig FileConfig)> _pipelines = [];

    public PipelineService()
    {
        var pipelinesDir = Path.Combine(AppContext.BaseDirectory, "pipelines");
        if (!Directory.Exists(pipelinesDir))
            return;

        foreach (var file in Directory.GetFiles(pipelinesDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = File.ReadAllText(file);
                var fileConfig = JsonConvert.DeserializeObject<PipelineFileConfig>(json);
                if (fileConfig?.Pipelines is null) continue;

                foreach (var pipeline in fileConfig.Pipelines)
                    _pipelines.Add((pipeline, fileConfig));
            }
            catch
            {
                // skip malformed files
            }
        }
    }

    public IReadOnlyList<(PipelineConfig Pipeline, PipelineFileConfig FileConfig)> GetAllPipelines()
        => _pipelines;

    public async Task<PipelineRunSummary> ExecuteAsync(string pipelineId)
    {
        var entry = _pipelines.FirstOrDefault(p => p.Pipeline.Id == pipelineId);
        if (entry == default)
            throw new KeyNotFoundException($"Pipeline '{pipelineId}' não encontrado.");

        var orchestrator = new PipelineOrchestrator(entry.FileConfig);
        return await orchestrator.ExecuteAsync(entry.Pipeline);
    }

    public byte[] GeneratePdfBytes(PipelineRunSummary summary)
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), $"report-{summary.CorrelationId}.pdf");
        try
        {
            PdfReportGenerator.Generate(summary, tmpPath);
            return File.ReadAllBytes(tmpPath);
        }
        finally
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
        }
    }
}
