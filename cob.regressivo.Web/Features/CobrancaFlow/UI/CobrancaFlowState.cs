using Cob.Regressivo.Web.Features.CobrancaFlow.Application.BuscarContratos;
using Cob.Regressivo.Web.Features.CobrancaFlow.Application.ExecutarFluxo;
using Cob.Regressivo.Web.Features.CobrancaFlow.Domain;

namespace Cob.Regressivo.Web.Features.CobrancaFlow.UI;

public class CobrancaFlowState
{
    public FlowStep         CurrentStep          { get; private set; } = FlowStep.EnterCpf;
    public string?          Cpf                  { get; private set; }
    public string           CorrelationId        { get; private set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public List<ContratoDto> Contratos            { get; private set; } = [];
    public List<ContratoDto> ContratosSelecionados { get; private set; } = [];
    public TipoFluxo?       FluxoSelecionado     { get; private set; }
    public ResultadoFluxoDto? Resultado           { get; private set; }
    public bool             IsLoading            { get; private set; }

    public event Action? OnChange;

    public void IniciarBusca(string cpf)
    {
        Cpf = cpf;
        IsLoading = true;
        Notify();
    }

    public void SetContratos(List<ContratoDto> contratos)
    {
        Contratos = contratos;
        ContratosSelecionados = [];
        IsLoading = false;
        CurrentStep = FlowStep.ViewContracts;
        Notify();
    }

    public void ConfirmarSelecao(List<ContratoDto> selecionados)
    {
        ContratosSelecionados = selecionados;
        CurrentStep = FlowStep.SelectFluxo;
        Notify();
    }

    public void VoltarParaContratos()
    {
        CurrentStep = FlowStep.ViewContracts;
        FluxoSelecionado = null;
        Notify();
    }

    public void IniciarExecucao(TipoFluxo fluxo)
    {
        FluxoSelecionado = fluxo;
        IsLoading = true;
        CurrentStep = FlowStep.Executing;
        Notify();
    }

    public void SetResultado(ResultadoFluxoDto resultado)
    {
        Resultado = resultado;
        IsLoading = false;
        CurrentStep = FlowStep.ViewResult;
        Notify();
    }

    public void Reiniciar()
    {
        CurrentStep = FlowStep.EnterCpf;
        Cpf = null;
        CorrelationId = Guid.NewGuid().ToString("N")[..8].ToUpper();
        Contratos = [];
        ContratosSelecionados = [];
        FluxoSelecionado = null;
        Resultado = null;
        IsLoading = false;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}