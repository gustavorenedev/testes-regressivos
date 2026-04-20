using Newtonsoft.Json.Linq;

namespace Cob.Regressivo.Core.Configuration;

public class BodyConfig
{
    /// <summary>json | form | xml | raw</summary>
    public string Type { get; set; } = "json";
    public JToken? Content { get; set; }
}
