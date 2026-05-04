#nullable enable

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class DSMSerializer
{
    public JsonSerializer JsonSerializer { get; }

    public DSMSerializer()
    {
        JsonSerializer = new JsonSerializer();
        JsonSerializer.Converters.Add(new Vector2Converter());
        JsonSerializer.Converters.Add(new Vector3Converter());
        JsonSerializer.Converters.Add(new Vector4Converter());
        JsonSerializer.Converters.Add(new QuaternionConverter());
        JsonSerializer.Converters.Add(new ColorConverter());
        JsonSerializer.Converters.Add(new Color32Converter());
    }

    public string Serialize(Dictionary<string, JToken> data, bool prettyPrint)
    {
        var root = new JObject();
        foreach (var (key, token) in data)
            root[key] = token;
        return root.ToString(prettyPrint ? Formatting.Indented : Formatting.None);
    }

    public Dictionary<string, JToken> Deserialize(string json)
    {
        var root = JObject.Parse(json);
        return root.Properties().ToDictionary(p => p.Name, p => p.Value);
    }
}
