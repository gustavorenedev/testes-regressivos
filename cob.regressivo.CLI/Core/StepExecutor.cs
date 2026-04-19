using System.Diagnostics;
using System.Text;
using Cob.Regressivo.CLI.Configuration;
using Cob.Regressivo.CLI.Http;
using Flurl.Http;

namespace Cob.Regressivo.CLI.Core;

public static class StepExecutor
{
    public static async Task<StepResult> ExecuteAsync(
        BuiltRequest request,
        RetryPolicyConfig retry,
        int timeoutSeconds)
    {
        StepResult? last = null;

        for (int attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            last = await TryExecuteAsync(request, timeoutSeconds);

            bool shouldRetry = !last.Success
                ? last.StatusCode == 0
                : retry.RetryOnStatusCodes.Contains(last.StatusCode);

            if (!shouldRetry || attempt == retry.MaxAttempts)
                return last;

            var delay = attempt <= retry.BackoffSeconds.Length
                ? retry.BackoffSeconds[attempt - 1]
                : retry.BackoffSeconds[^1];

            await Task.Delay(TimeSpan.FromSeconds(delay));
        }

        return last!;
    }

    private static async Task<StepResult> TryExecuteAsync(BuiltRequest request, int timeoutSeconds)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // WithHeaders(Dictionary) no Flurl 4.x trata o dict como objeto anônimo.
            // Usar WithHeader individualmente garante que cada par chave/valor seja setado.
            IFlurlRequest furl = new FlurlRequest(request.Url)
                .WithTimeout(timeoutSeconds)
                .AllowAnyHttpStatus();

            foreach (var (key, value) in request.Headers)
                furl = furl.WithHeader(key, value);

            HttpContent? content = request.Body != null
                ? new StringContent(request.Body, Encoding.UTF8, request.ContentType ?? "application/json")
                : null;

            var response = await furl.SendAsync(new HttpMethod(request.Method), content);
            sw.Stop();

            var body = await response.GetStringAsync();
            // Headers HTTP podem ter chaves duplicadas (ex: Vary, Set-Cookie).
            // Agrupa e une os valores com vírgula conforme RFC 7230.
            var headers = response.Headers
                .GroupBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(h => h.Value)), StringComparer.OrdinalIgnoreCase);

            return new StepResult(response.StatusCode, body, headers, sw.Elapsed, true, null);
        }
        catch (FlurlHttpTimeoutException ex)
        {
            sw.Stop();
            return new StepResult(0, string.Empty, new(), sw.Elapsed, false, $"Timeout: {ex.Message}");
        }
        catch (FlurlHttpException ex)
        {
            sw.Stop();
            var body = await ex.GetResponseStringAsync() ?? string.Empty;
            return new StepResult(ex.StatusCode ?? 0, body, new(), sw.Elapsed, false, ex.Message);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StepResult(0, string.Empty, new(), sw.Elapsed, false, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
