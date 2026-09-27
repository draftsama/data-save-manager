#nullable enable

using Newtonsoft.Json;

namespace DataSaveManager
{
    internal sealed class DSMSerializer
    {
        public JsonSerializer JsonSerializer { get; }

        public DSMSerializer()
        {
            JsonSerializer = new JsonSerializer();
            JsonSerializer.Converters.Add(new Vector2Converter());
            JsonSerializer.Converters.Add(new Vector3Converter());
            JsonSerializer.Converters.Add(new ColorConverter());
        }
    }
}
