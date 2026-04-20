using Cob.Regressivo.Web.Features.CobrancaFlow.Domain;

namespace Cob.Regressivo.Web.Features.CobrancaFlow.Infrastructure;

public class ContratoRepositoryMock
{
    // CPFs de teste: 12345678909 / 98765432100
    private static readonly List<Contrato> _dados =
    [
        new("CTR-001", "12345678909", "Empréstimo Pessoal",       5_000m, 12,  416.67m, StatusContrato.EmAtraso, new DateTime(2024, 3, 15)),
        new("CTR-002", "12345678909", "Financiamento de Veículo", 25_000m, 36, 694.44m, StatusContrato.Vencido,  new DateTime(2023, 12, 10)),
        new("CTR-003", "12345678909", "Cartão de Crédito",         1_500m,  3,  500.00m, StatusContrato.EmAtraso, new DateTime(2024, 4, 1)),
        new("CTR-004", "98765432100", "Crédito Consignado",        8_000m, 24,  333.33m, StatusContrato.Ativo,    new DateTime(2025, 6, 30)),
        new("CTR-005", "98765432100", "Empréstimo Pessoal",        3_200m, 18,  177.78m, StatusContrato.EmAtraso, new DateTime(2024, 2, 28)),
    ];

    public async Task<IEnumerable<Contrato>> BuscarPorCpfAsync(string cpf)
    {
        await Task.Delay(600); // simula latência de API
        var cpfNumeros = new string(cpf.Where(char.IsDigit).ToArray());
        return _dados.Where(c => c.Cpf == cpfNumeros);
    }
}