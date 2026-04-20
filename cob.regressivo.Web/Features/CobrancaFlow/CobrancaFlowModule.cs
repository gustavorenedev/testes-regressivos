using Cob.Regressivo.Web.Features.CobrancaFlow.Infrastructure;
using Cob.Regressivo.Web.Features.CobrancaFlow.UI;

namespace Cob.Regressivo.Web.Features.CobrancaFlow;

public static class CobrancaFlowModule
{
    public static IServiceCollection AddCobrancaFlow(this IServiceCollection services)
    {
        services.AddScoped<ContratoRepositoryMock>();
        services.AddScoped<CobrancaFlowState>();
        return services;
    }
}
