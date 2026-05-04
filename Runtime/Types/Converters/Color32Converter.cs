#nullable enable

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class Color32Converter : JsonConverter<Color32>
{
    public override void WriteJson(JsonWriter writer, Color32 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("r"); writer.WriteValue(value.r);
        writer.WritePropertyName("g"); writer.WriteValue(value.g);
        writer.WritePropertyName("b"); writer.WriteValue(value.b);
        writer.WritePropertyName("a"); writer.WriteValue(value.a);
        writer.WriteEndObject();
    }

    public override Color32 ReadJson(JsonReader reader, Type objectType, Color32 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        return new Color32(
            obj["r"]?.Value<byte>() ?? 0,
            obj["g"]?.Value<byte>() ?? 0,
            obj["b"]?.Value<byte>() ?? 0,
            obj["a"]?.Value<byte>() ?? 255);
    }
}
