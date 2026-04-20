namespace Cob.Regressivo.Web.Features.CobrancaFlow.Domain;

public record Contrato(
    string Id,
    string Cpf,
    string Descricao,
    decimal ValorTotal,
    int NumeroParcelas,
    decimal ValorParcela,
    StatusContrato Status,
    DateTime DataVencimento);