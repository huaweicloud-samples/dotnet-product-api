using System.Text;
using System.Text.Json;

namespace ProductApi.Tests;

internal static class TestSupport
{
    public static Stream Event(
        string method,
        string path,
        object? body = null,
        bool base64 = false,
        IReadOnlyDictionary<string, string>? pathParameters = null,
        IReadOnlyDictionary<string, string>? query = null)
    {
        string? bodyText = body is null ? null : JsonSerializer.Serialize(body);
        if (base64 && bodyText is not null)
        {
            bodyText = Convert.ToBase64String(Encoding.UTF8.GetBytes(bodyText));
        }
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            httpMethod = method,
            path,
            pathParameters,
            queryStringParameters = query,
            body = bodyText,
            isBase64Encoded = base64,
        });
        return new MemoryStream(payload);
    }

    public static JsonDocument Response(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return JsonDocument.Parse(reader.ReadToEnd());
    }

    public static int Status(JsonDocument response)
    {
        return response.RootElement.GetProperty("statusCode").GetInt32();
    }

    public static string Body(JsonDocument response)
    {
        return response.RootElement.GetProperty("body").GetString() ?? string.Empty;
    }
}
