using MediatR;
using Cob.Regressivo.Web.Features.CobrancaFlow.Domain;

namespace Cob.Regressivo.Web.Features.CobrancaFlow.Application.ExecutarFluxo;

public class ExecutarFluxoHandler : IRequestHandler<ExecutarFluxoCommand, ResultadoFluxoDto>
{
    public async Task<ResultadoFluxoDto> Handle(ExecutarFluxoCommand request, CancellationToken ct)
    {
        await Task.Delay(900, ct); // simula latência de API

        var total = request.ContratosSelecionados.Sum(c => c.ValorTotal);
        var ids   = string.Join(", ", request.ContratosSelecionados.Select(c => c.Id));

        return request.TipoFluxo switch
        {
            TipoFluxo.PagamentoVista   => PagamentoVista(total, ids),
            TipoFluxo.Renegociacao     => Renegociacao(total, ids),
            TipoFluxo.PagamentoParcial => PagamentoParcial(total, ids),
            _ => new ResultadoFluxoDto(false, "Tipo de fluxo não reconhecido.", null)
        };
    }

    private static ResultadoFluxoDto PagamentoVista(decimal total, string ids)
    {
        var desconto = total * 0.20m;
        var final    = total - desconto;
        return new ResultadoFluxoDto(true,
            "Pagamento à vista processado com sucesso!",
            $"Contratos: {ids}\nValor original : {total:C}\nDesconto (20%) : {desconto:C}\nValor final    : {final:C}",
            final);
    }

    private static ResultadoFluxoDto Renegociacao(decimal total, string ids)
    {
        var parcela = total / 36;
        return new ResultadoFluxoDto(true,
            "Renegociação realizada com sucesso!",
            $"Contratos: {ids}\nNovo plano     : 36 parcelas de {parcela:C}\nTotal          : {total:C}",
            total);
    }

    private static ResultadoFluxoDto PagamentoParcial(decimal total, string ids)
    {
        var pago    = total * 0.30m;
        var restante = total - pago;
        return new ResultadoFluxoDto(true,
            "Pagamento parcial registrado com sucesso!",
            $"Contratos: {ids}\nValor pago (30%): {pago:C}\nSaldo restante  : {restante:C}",
            pago);
    }
}