namespace Cob.Regressivo.Web.Features.CobrancaFlow.Application.BuscarContratos;

public record ContratoDto(
    string Id,
    string Descricao,
    decimal ValorTotal,
    int NumeroParcelas,
    decimal ValorParcela,
    string Status,
    DateTime DataVencimento);