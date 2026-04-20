using Cob.Regressivo.Core.Assertions;
using Cob.Regressivo.Core.Configuration;
using Cob.Regressivo.Core.Extraction;
using Cob.Regressivo.Core.Http;

namespace Cob.Regressivo.Core.Engine;

public class PipelineOrchestrator
{
    public async Task<(List<ExecutionRecord> Records, string CorrelationId)> ExecuteAsync(
        PipelineFileConfig fileConfig,
        PipelineConfig pipeline,
        Action<string>? onProgress = null)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var context       = new PipelineExecutionContext(correlationId, fileConfig.Variables, fileConfig.Globals);
        var records       = new List<ExecutionRecord>();
        var failedSteps   = new HashSet<string>();

        onProgress?.Invoke($"CorrelationId: {correlationId}");

        var orderedSteps = TopologicalSorter.Sort(pipeline.Steps);

        foreach (var step in orderedSteps)
        {
            onProgress?.Invoke($"→ {step.Name} ({step.Id})");

            var failedDep = step.DependsOn.FirstOrDefault(d => failedSteps.Contains(d));
            if (failedDep != null)
            {
                onProgress?.Invoke($"  Ignorado — dependência falhou: {failedDep}");
                failedSteps.Add(step.Id);
                records.Add(new ExecutionRecord
                {
                    StepId        = step.Id,
                    StepName      = step.Name,
                    CorrelationId = correlationId,
                    Timestamp     = DateTime.UtcNow,
                    Success       = false,
                    ErrorMessage  = $"Ignorado: dependência '{failedDep}' falhou",
                });
                continue;
            }

            BuiltRequest request;
            try
            {
                request = RequestBuilder.Build(step, context);
            }
            catch (Exception ex)
            {
                onProgress?.Invoke($"  Erro ao montar request: {ex.Message}");
                failedSteps.Add(step.Id);
                records.Add(new ExecutionRecord
                {
                    StepId        = step.Id,
                    StepName      = step.Name,
                    CorrelationId = correlationId,
                    Timestamp     = DateTime.UtcNow,
                    Success       = false,
                    ErrorMessage  = ex.Message,
                });
                if (pipeline.OnError == "stop") break;
                continue;
            }

            onProgress?.Invoke($"  {request.Method} {request.Url}");

            var retryConfig = step.RetryPolicy ?? fileConfig.Globals.RetryPolicy;
            var result = await StepExecutor.ExecuteAsync(request, retryConfig, fileConfig.Globals.TimeoutSeconds);

            if (result.StatusCode == 0 && result.ErrorMessage != null)
                onProgress?.Invoke($"  Erro: {result.ErrorMessage}");

            var assertions = AssertionEvaluator.Evaluate(result.StatusCode, result.Body, step.Assertions);
            var allPassed  = assertions.All(a => a.Passed);

            onProgress?.Invoke($"  Status: {result.StatusCode} | {result.Duration.TotalMilliseconds:F0}ms");

            foreach (var a in assertions)
                onProgress?.Invoke($"  {(a.Passed ? "✓" : "✗")} {a.Message}");

            if (result.Success && !string.IsNullOrWhiteSpace(result.Body))
            {
                var extracted = JsonPathExtractor.Extract(result.Body, step.Extract);
                context.AddExtractions(step.Id, extracted);
                if (extracted.Count > 0)
                    onProgress?.Invoke($"  Extraído: {string.Join(", ", extracted.Keys)}");
            }

            var stepOk = result.Success && allPassed;
            if (!stepOk) failedSteps.Add(step.Id);

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
                ErrorMessage       = result.ErrorMessage,
            });

            if (!stepOk && pipeline.OnError == "stop")
            {
                onProgress?.Invoke("Pipeline interrompido (onError: stop)");
                break;
            }
        }

        return (records, correlationId);
    }
}
