using Cob.Regressivo.Core.Configuration;
using MediatR;

namespace Cob.Regressivo.Web.Features.PipelineRunner.Application.CarregarPipelines;

public record CarregarPipelinesQuery : IRequest<List<PipelineEntry>>;

public record PipelineEntry(PipelineConfig Pipeline, PipelineFileConfig FileConfig, string SourceFile);
