#nullable enable

using Newtonsoft.Json.Linq;

public interface IDSMMigration
{
    int FromVersion { get; }
    void Migrate(JObject data);
}
