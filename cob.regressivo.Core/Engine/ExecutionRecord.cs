namespace Cob.Regressivo.Core.Engine;

public record AssertionResult(bool Passed, string Message);

public class ExecutionRecord
{
    public string StepId { get; init; } = string.Empty;
    public string StepName { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string RequestMethod { get; init; } = string.Empty;
    public string RequestUrl { get; init; } = string.Empty;
    public Dictionary<string, string> RequestHeaders { get; init; } = new();
    public string? RequestBody { get; init; }
    public int ResponseStatusCode { get; init; }
    public Dictionary<string, string> ResponseHeaders { get; init; } = new();
    public string? ResponseBody { get; init; }
    public TimeSpan Duration { get; init; }
    public List<AssertionResult> Assertions { get; init; } = [];
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
