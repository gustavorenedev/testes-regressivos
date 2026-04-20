using Cob.Regressivo.Core.Engine;

namespace Cob.Regressivo.Web.Features.PipelineRunner;

public static class PipelineRunnerModule
{
    public static IServiceCollection AddPipelineRunner(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PipelineRunnerOptions>(configuration.GetSection("PipelineRunner"));
        services.AddSingleton<PipelineOrchestrator>();
        return services;
    }
}
