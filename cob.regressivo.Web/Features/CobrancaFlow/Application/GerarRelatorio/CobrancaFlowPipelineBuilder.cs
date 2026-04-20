using Cob.Regressivo.Core.Configuration;
using Cob.Regressivo.Core.Engine;
using Cob.Regressivo.Web.Features.CobrancaFlow.Application.BuscarContratos;
using Cob.Regressivo.Web.Features.CobrancaFlow.Application.ExecutarFluxo;
using Cob.Regressivo.Web.Features.CobrancaFlow.Domain;

namespace Cob.Regressivo.Web.Features.CobrancaFlow.Application.GerarRelatorio;

/// <summary>
/// Constrói os objetos do Core (PipelineConfig + ExecutionRecords) a partir dos dados
/// coletados pelo wizard de cobrança, permitindo gerar o PDF no mesmo formato da CLI.
/// </summary>
public static class CobrancaFlowPipelineBuilder
{
    public record BuildResult(List<ExecutionRecord> Records, PipelineConfig Config);

    public static BuildResult Build(
        string cpf,
        string correlationId,
        List<ContratoDto> contratosEncontrados,
        List<ContratoDto> contratosSelecionados,
        TipoFluxo tipoFluxo,
        ResultadoFluxoDto resultado)
    {
        var now            = DateTime.Now;
        var cpfFormatado   = FormatCpf(cpf);
        var totalSelecionado = contratosSelecionados.Sum(c => c.ValorTotal);
        var tipoLabel      = TipoLabel(tipoFluxo);

        // --- Step 1: Buscar Contratos ---
        var step1Body = string.Join("\n", contratosEncontrados.Select(c =>
            $"  [{c.Id}] {c.Descricao} | Valor: {c.ValorTotal:C} | Venc: {c.DataVencimento:dd/MM/yyyy} | Status: {c.Status}"));

        var record1 = new ExecutionRecord
        {
            StepId             = "buscar_contratos",
            StepName           = "Buscar Contratos por CPF",
            CorrelationId      = correlationId,
            Timestamp          = now,
            RequestMethod      = "GET",
            RequestUrl         = $"/api/contratos?cpf={cpf}",
            RequestHeaders     = new() { ["Accept"] = "application/json" },
            ResponseStatusCode = contratosEncontrados.Count > 0 ? 200 : 204,
            ResponseBody       = contratosEncontrados.Count > 0
                ? $"{contratosEncontrados.Count} contrato(s):\n{step1Body}"
                : "Nenhum contrato encontrado.",
            Duration           = TimeSpan.FromMilliseconds(120),
            Assertions         = [new(contratosEncontrados.Count > 0, $"Contratos encontrados para CPF {cpfFormatado}: {contratosEncontrados.Count}")],
            Success            = contratosEncontrados.Count > 0,
            ErrorMessage       = contratosEncontrados.Count == 0 ? "Nenhum contrato localizado." : null,
        };

        // --- Step 2: Confirmar Seleção ---
        var step2Body = string.Join("\n", contratosSelecionados.Select(c =>
            $"  [{c.Id}] {c.Descricao} | Valor: {c.ValorTotal:C}"));

        var record2 = new ExecutionRecord
        {
            StepId             = "confirmar_selecao",
            StepName           = "Confirmar Seleção de Contratos",
            CorrelationId      = correlationId,
            Timestamp          = now.AddMilliseconds(150),
            RequestMethod      = "POST",
            RequestUrl         = "/api/contratos/selecao",
            RequestHeaders     = new() { ["Content-Type"] = "application/json", ["X-Correlation-Id"] = correlationId },
            RequestBody        = $"CPF: {cpfFormatado}\nContratos ({contratosSelecionados.Count}):\n{step2Body}\nTotal: {totalSelecionado:C}",
            ResponseStatusCode = 200,
            ResponseBody       = $"{contratosSelecionados.Count} contrato(s) confirmado(s). Total: {totalSelecionado:C}",
            Duration           = TimeSpan.FromMilliseconds(45),
            Assertions         = [new(contratosSelecionados.Count > 0, $"Ao menos 1 contrato selecionado: {contratosSelecionados.Count}"),
                                   new(totalSelecionado > 0, $"Valor total positivo: {totalSelecionado:C}")],
            Success            = contratosSelecionados.Count > 0,
        };

        // --- Step 3: Executar Fluxo ---
        var responseBody = resultado.Detalhes != null
            ? $"{resultado.Mensagem}\n\n{resultado.Detalhes}"
            : resultado.Mensagem;
        if (resultado.ValorAcordado.HasValue)
            responseBody += $"\n\nValor acordado: {resultado.ValorAcordado.Value:C}";

        var assertions3 = new List<AssertionResult>
        {
            new(resultado.Sucesso, $"Fluxo '{tipoLabel}' executado com sucesso"),
        };
        if (resultado.ValorAcordado.HasValue)
            assertions3.Add(new(resultado.ValorAcordado.Value > 0, $"Valor acordado positivo: {resultado.ValorAcordado.Value:C}"));

        var record3 = new ExecutionRecord
        {
            StepId             = "executar_fluxo",
            StepName           = $"Executar Fluxo — {tipoLabel}",
            CorrelationId      = correlationId,
            Timestamp          = now.AddMilliseconds(200),
            RequestMethod      = "POST",
            RequestUrl         = $"/api/fluxo/{tipoFluxo.ToString().ToLower()}",
            RequestHeaders     = new() { ["Content-Type"] = "application/json", ["X-Correlation-Id"] = correlationId },
            RequestBody        = $"CPF: {cpfFormatado}\nTipo: {tipoLabel}\nContratos: {contratosSelecionados.Count} | Total: {totalSelecionado:C}",
            ResponseStatusCode = resultado.Sucesso ? 200 : 422,
            ResponseBody       = responseBody,
            Duration           = TimeSpan.FromMilliseconds(310),
            Assertions         = assertions3,
            Success            = resultado.Sucesso,
            ErrorMessage       = resultado.Sucesso ? null : resultado.Mensagem,
        };

        // --- PipelineConfig (metadata para o PDF) ---
        var config = new PipelineConfig
        {
            Id          = "cobranca_flow",
            Name        = "Gestão de Cobrança — Negociação",
            Description = "Fluxo de negociação de contratos em aberto",
            Tags        = ["cobranca", "negociacao"],
            Report = new ReportConfig
            {
                Title                = "Relatório de Negociação de Cobrança",
                IncludeRequestBody   = true,
                IncludeResponseBody  = true,
                SensitiveFields      = [],
            },
        };

        return new BuildResult([record1, record2, record3], config);
    }

    private static string FormatCpf(string cpf)
    {
        var d = new string(cpf.Where(char.IsDigit).ToArray());
        return d.Length == 11 ? $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}" : cpf;
    }

    private static string TipoLabel(TipoFluxo tipo) => tipo switch
    {
        TipoFluxo.PagamentoVista   => "Pagamento à Vista",
        TipoFluxo.Renegociacao     => "Renegociação",
        TipoFluxo.PagamentoParcial => "Pagamento Parcial",
        _                          => tipo.ToString(),
    };
}
