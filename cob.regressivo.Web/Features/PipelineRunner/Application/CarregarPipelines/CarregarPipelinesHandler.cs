using Cob.Regressivo.Core.Configuration;
using MediatR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Cob.Regressivo.Web.Features.PipelineRunner.Application.CarregarPipelines;

public class CarregarPipelinesHandler : IRequestHandler<CarregarPipelinesQuery, List<PipelineEntry>>
{
    private readonly PipelineRunnerOptions _options;

    public CarregarPipelinesHandler(IOptions<PipelineRunnerOptions> options)
        => _options = options.Value;

    public async Task<List<PipelineEntry>> Handle(CarregarPipelinesQuery request, CancellationToken ct)
    {
        var dir = ResolvePath(_options.PipelinesDirectory);
        if (!Directory.Exists(dir)) return [];

        var result = new List<PipelineEntry>();

        foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json       = await File.ReadAllTextAsync(file, ct);
                var fileConfig = JsonConvert.DeserializeObject<PipelineFileConfig>(json);
                if (fileConfig?.Pipelines is null) continue;

                foreach (var pipeline in fileConfig.Pipelines)
                    result.Add(new PipelineEntry(pipeline, fileConfig, Path.GetFileName(file)));
            }
            catch { /* arquivo inválido — ignorar */ }
        }

        return result;
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
