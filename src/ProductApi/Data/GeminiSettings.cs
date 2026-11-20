namespace ProductApi.Data;

public sealed class GeminiSettings
{
    public required Uri Endpoint { get; init; }
    public required string Region { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string TableName { get; init; }

    public static GeminiSettings FromEnvironment()
    {
        string endpoint = Required("GEMINI_ENDPOINT");
        string protocol = Environment.GetEnvironmentVariable("GEMINI_PROTOCOL") ?? "http";
        string portText = Environment.GetEnvironmentVariable("GEMINI_PORT") ?? "8000";
        if (!int.TryParse(portText, out int port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("GEMINI_PORT must be a valid TCP port");
        }

        string raw = endpoint.Contains("://", StringComparison.Ordinal)
            ? endpoint
            : protocol + "://" + endpoint;
        var builder = new UriBuilder(raw)
        {
            Port = port,
        };
        if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            builder.Path += "/";
        }

        return new GeminiSettings
        {
            Endpoint = builder.Uri,
            Region = Environment.GetEnvironmentVariable("GEMINI_REGION") ?? "cn-north-4",
            AccessKey = Required("GEMINI_ACCESS_KEY"),
            SecretKey = Required("GEMINI_SECRET_KEY"),
            TableName = Environment.GetEnvironmentVariable("PRODUCT_TABLE_NAME") ?? "products",
        };
    }

    private static string Required(string name)
    {
        string value = Environment.GetEnvironmentVariable(name) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(name + " is required");
        }
        return value;
    }
}
