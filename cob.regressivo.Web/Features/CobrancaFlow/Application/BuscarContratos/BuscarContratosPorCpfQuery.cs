using MediatR;

namespace Cob.Regressivo.Web.Features.CobrancaFlow.Application.BuscarContratos;

public record BuscarContratosPorCpfQuery(string Cpf) : IRequest<List<ContratoDto>>;