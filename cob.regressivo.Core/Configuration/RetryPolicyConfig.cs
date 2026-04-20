namespace Cob.Regressivo.Core.Configuration;

public class RetryPolicyConfig
{
    public int MaxAttempts { get; set; } = 3;
    public int[] BackoffSeconds { get; set; } = [1, 2, 5];
    public int[] RetryOnStatusCodes { get; set; } = [429, 500, 502, 503];
}
