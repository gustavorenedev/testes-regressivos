namespace Cob.Regressivo.Core.Engine;

public record StepResult(
    int StatusCode,
    string Body,
    Dictionary<string, string> Headers,
    TimeSpan Duration,
    bool Success,
    string? ErrorMessage);
