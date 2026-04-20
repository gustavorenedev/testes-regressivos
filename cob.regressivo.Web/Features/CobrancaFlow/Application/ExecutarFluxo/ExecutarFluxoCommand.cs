using MediatR;
using Cob.Regressivo.Web.Features.CobrancaFlow.Application.BuscarContratos;
using Cob.Regressivo.Web.Features.CobrancaFlow.Domain;

namespace Cob.Regressivo.Web.Features.CobrancaFlow.Application.ExecutarFluxo;

public record ExecutarFluxoCommand(
    string Cpf,
    List<ContratoDto> ContratosSelecionados,
    TipoFluxo TipoFluxo) : IRequest<ResultadoFluxoDto>;