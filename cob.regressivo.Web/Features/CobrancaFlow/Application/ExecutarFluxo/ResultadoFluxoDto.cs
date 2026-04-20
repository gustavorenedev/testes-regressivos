namespace Cob.Regressivo.Web.Features.CobrancaFlow.Application.ExecutarFluxo;

public record ResultadoFluxoDto(
    bool Sucesso,
    string Mensagem,
    string? Detalhes,
    decimal? ValorAcordado = null);