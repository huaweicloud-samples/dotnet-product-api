using System.Text;
using System.Text.Json;

namespace ProductApi;

public sealed class ApiGatewayEvent
{
    public string HttpMethod { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public IReadOnlyDictionary<string, string> PathParameters { get; private set; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> QueryStringParameters { get; private set; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string Body { get; private set; } = string.Empty;

    public static ApiGatewayEvent Parse(Stream input)
    {
        using var reader = new StreamReader(input ?? Stream.Null, Encoding.UTF8, true, 1024, leaveOpen: true);
        string payload = reader.ReadToEnd();
        using JsonDocument document = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
        JsonElement root = document.RootElement;

        bool encoded = GetBoolean(root, "isBase64Encoded");
        string body = GetString(root, "body");
        if (encoded && !string.IsNullOrEmpty(body))
        {
            body = Encoding.UTF8.GetString(Convert.FromBase64String(body));
        }

        return new ApiGatewayEvent
        {
            HttpMethod = GetString(root, "httpMethod"),
            Path = GetString(root, "path"),
            PathParameters = GetStringMap(root, "pathParameters"),
            QueryStringParameters = GetStringMap(root, "queryStringParameters"),
            Body = body,
        };
    }

    public string GetProductId()
    {
        if (PathParameters.TryGetValue("id", out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string path = Path.TrimEnd('/');
        int separator = path.LastIndexOf('/');
        return separator >= 0 && separator < path.Length - 1
            ? Uri.UnescapeDataString(path[(separator + 1)..])
            : string.Empty;
    }

    private static string GetString(JsonElement root, string name)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    private static bool GetBoolean(JsonElement root, string name)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out JsonElement value)
            && (value.ValueKind == JsonValueKind.True
                || value.ValueKind == JsonValueKind.String
                && bool.TryParse(value.GetString(), out bool parsed)
                && parsed);
    }

    private static IReadOnlyDictionary<string, string> GetStringMap(JsonElement root, string name)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(name, out JsonElement map)
            || map.ValueKind != JsonValueKind.Object)
        {
            return values;
        }

        foreach (JsonProperty property in map.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.ToString();
        }
        return values;
    }
}
