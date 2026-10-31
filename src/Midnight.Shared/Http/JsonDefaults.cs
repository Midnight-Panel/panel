using System.Text.Json;
using System.Text.Json.Serialization;

namespace Midnight.Shared.Http;

/// <summary>Json settings used by the api, the client and the daemon: camelCase and enums as strings</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Create();

    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Converters.Add(new JsonStringEnumConverter());
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Apply(options);
        return options;
    }
}
