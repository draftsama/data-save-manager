#nullable enable

using Newtonsoft.Json;

namespace OperatorConsole
{
    internal sealed class OCSerializer
    {
        public JsonSerializer JsonSerializer { get; }

        public OCSerializer()
        {
            JsonSerializer = new JsonSerializer();
            JsonSerializer.Converters.Add(new Vector2Converter());
            JsonSerializer.Converters.Add(new Vector3Converter());
            JsonSerializer.Converters.Add(new ColorConverter());
        }
    }
}
