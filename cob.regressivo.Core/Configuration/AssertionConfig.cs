namespace Cob.Regressivo.Core.Configuration;

public class AssertionConfig
{
    public string? Path { get; set; }
    public string Operator { get; set; } = "notEmpty";
    public string? Value { get; set; }
    public int? StatusCode { get; set; }
}
