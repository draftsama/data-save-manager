#nullable enable

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class Vector4Converter : JsonConverter<Vector4>
{
    public override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(value.x);
        writer.WritePropertyName("y"); writer.WriteValue(value.y);
        writer.WritePropertyName("z"); writer.WriteValue(value.z);
        writer.WritePropertyName("w"); writer.WriteValue(value.w);
        writer.WriteEndObject();
    }

    public override Vector4 ReadJson(JsonReader reader, Type objectType, Vector4 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        return new Vector4(
            obj["x"]?.Value<float>() ?? 0f,
            obj["y"]?.Value<float>() ?? 0f,
            obj["z"]?.Value<float>() ?? 0f,
            obj["w"]?.Value<float>() ?? 0f);
    }
}
