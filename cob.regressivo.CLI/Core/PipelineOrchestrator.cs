using Cob.Regressivo.CLI.Assertions;
using Cob.Regressivo.CLI.Configuration;
using Cob.Regressivo.CLI.Extraction;
using Cob.Regressivo.CLI.Http;
using Spectre.Console;

namespace Cob.Regressivo.CLI.Core;

public class PipelineOrchestrator
{
    private readonly PipelineFileConfig _fileConfig;

    public PipelineOrchestrator(PipelineFileConfig fileConfig)
        => _fileConfig = fileConfig;

    public async Task<(List<ExecutionRecord> Records, string CorrelationId)> ExecuteAsync(PipelineConfig pipeline)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var context = new PipelineExecutionContext(correlationId, _fileConfig.Variables, _fileConfig.Globals);
        var records = new List<ExecutionRecord>();
        var failedSteps = new HashSet<string>();

        AnsiConsole.MarkupLine($"[grey]CorrelationId:[/] [cyan]{correlationId}[/]");

        var orderedSteps = TopologicalSorter.Sort(pipeline.Steps);

        foreach (var step in orderedSteps)
        {
            AnsiConsole.MarkupLine($"\n[bold]→[/] [yellow]{Markup.Escape(step.Name)}[/] [grey]({step.Id})[/]");

            // Pula step se alguma dependência falhou
            var failedDep = step.DependsOn.FirstOrDefault(d => failedSteps.Contains(d));
            if (failedDep != null)
            {
                AnsiConsole.MarkupLine($"  [grey]Ignorado — dependência falhou: {failedDep}[/]");
                failedSteps.Add(step.Id);
                records.Add(new ExecutionRecord
                {
                    StepId        = step.Id,
                    StepName      = step.Name,
                    CorrelationId = correlationId,
                    Timestamp     = DateTime.UtcNow,
                    Success       = false,
                    ErrorMessage  = $"Ignorado: dependência '{failedDep}' falhou"
                });
                continue;
            }

            // Tenta montar o request (template pode falhar se variável não resolvida)
            BuiltRequest request;
            try
            {
                request = RequestBuilder.Build(step, context);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [red]Erro ao montar request:[/] {Markup.Escape(ex.Message)}");
                failedSteps.Add(step.Id);
                records.Add(new ExecutionRecord
                {
                    StepId        = step.Id,
                    StepName      = step.Name,
                    CorrelationId = correlationId,
                    Timestamp     = DateTime.UtcNow,
                    Success       = false,
                    ErrorMessage  = ex.Message
                });
                if (pipeline.OnError == "stop") break;
                continue;
            }

            var retryConfig = step.RetryPolicy ?? _fileConfig.Globals.RetryPolicy;

            var result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"  {request.Method} {Markup.Escape(request.Url)}...", _ =>
                    StepExecutor.ExecuteAsync(request, retryConfig, _fileConfig.Globals.TimeoutSeconds));

            // Exibe erro de rede/conexão quando status = 0
            if (result.StatusCode == 0 && result.ErrorMessage != null)
                AnsiConsole.MarkupLine($"  [red]Erro:[/] {Markup.Escape(result.ErrorMessage)}");

            var assertions = AssertionEvaluator.Evaluate(result.StatusCode, result.Body, step.Assertions);
            var allPassed  = assertions.All(a => a.Passed);

            var statusColor = result.StatusCode is >= 200 and < 300 ? "green" : "red";
            AnsiConsole.MarkupLine($"  Status: [{statusColor}]{result.StatusCode}[/] | {result.Duration.TotalMilliseconds:F0}ms");

            foreach (var a in assertions)
                AnsiConsole.MarkupLine($"  [{(a.Passed ? "green" : "red")}]{(a.Passed ? "✓" : "✗")}[/] {Markup.Escape(a.Message)}");

            if (result.Success && !string.IsNullOrWhiteSpace(result.Body))
            {
                var extracted = JsonPathExtractor.Extract(result.Body, step.Extract);
                context.AddExtractions(step.Id, extracted);
                if (extracted.Count > 0)
                    AnsiConsole.MarkupLine($"  [grey]Extraído: {string.Join(", ", extracted.Keys)}[/]");
            }

            var stepOk = result.Success && allPassed;
            if (!stepOk)
                failedSteps.Add(step.Id);

            records.Add(new ExecutionRecord
            {
                StepId             = step.Id,
                StepName           = step.Name,
                CorrelationId      = correlationId,
                Timestamp          = DateTime.UtcNow,
                RequestMethod      = request.Method,
                RequestUrl         = request.Url,
                RequestHeaders     = request.Headers,
                RequestBody        = request.Body,
                ResponseStatusCode = result.StatusCode,
                ResponseHeaders    = result.Headers,
                ResponseBody       = result.Body,
                Duration           = result.Duration,
                Assertions         = assertions,
                Success            = stepOk,
                ErrorMessage       = result.ErrorMessage
            });

            if (!stepOk && pipeline.OnError == "stop")
            {
                AnsiConsole.MarkupLine("[red]Pipeline interrompido (onError: stop)[/]");
                break;
            }
        }

        return (records, correlationId);
    }
}
