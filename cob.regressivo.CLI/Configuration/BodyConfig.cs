using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cob.Regressivo.CLI.Configuration;

[JsonConverter(typeof(BodyConfigConverter))]
public class BodyConfig
{
    /// <summary>json | form | xml | raw</summary>
    public string Type { get; set; } = "json";
    public JToken? Content { get; set; }
}

public class BodyConfigConverter : JsonConverter<BodyConfig>
{
    public override BodyConfig? ReadJson(JsonReader reader, Type objectType, BodyConfig? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;

        var jObject = JObject.Load(reader);

        if (jObject.ContainsKey("content"))
        {
            return new BodyConfig
            {
                Type    = jObject["type"]?.Value<string>() ?? "json",
                Content = jObject["content"]
            };
        }

        return new BodyConfig
        {
            Type    = "json",
            Content = jObject
        };
    }

    public override void WriteJson(JsonWriter writer, BodyConfig? value, JsonSerializer serializer)
    {
        if (value == null) { writer.WriteNull(); return; }
        new JObject { ["type"] = value.Type, ["content"] = value.Content }.WriteTo(writer);
    }
}
