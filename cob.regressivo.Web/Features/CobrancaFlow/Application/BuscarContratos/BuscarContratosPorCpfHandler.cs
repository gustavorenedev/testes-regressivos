using MediatR;
using Cob.Regressivo.Web.Features.CobrancaFlow.Infrastructure;

namespace Cob.Regressivo.Web.Features.CobrancaFlow.Application.BuscarContratos;

public class BuscarContratosPorCpfHandler : IRequestHandler<BuscarContratosPorCpfQuery, List<ContratoDto>>
{
    private readonly ContratoRepositoryMock _repository;

    public BuscarContratosPorCpfHandler(ContratoRepositoryMock repository)
        => _repository = repository;

    public async Task<List<ContratoDto>> Handle(BuscarContratosPorCpfQuery request, CancellationToken ct)
    {
        var contratos = await _repository.BuscarPorCpfAsync(request.Cpf);
        return contratos.Select(c => new ContratoDto(
            c.Id, c.Descricao, c.ValorTotal,
            c.NumeroParcelas, c.ValorParcela,
            c.Status.ToString(), c.DataVencimento))
            .ToList();
    }
}
