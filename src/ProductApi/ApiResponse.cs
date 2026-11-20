using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProductApi;

public sealed class ApiResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; }

    [JsonPropertyName("headers")]
    public IReadOnlyDictionary<string, string> Headers { get; init; }
        = new Dictionary<string, string> { ["Content-Type"] = "application/json" };

    [JsonPropertyName("isBase64Encoded")]
    public bool IsBase64Encoded { get; init; }

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    public static ApiResponse Json(int statusCode, object body)
    {
        return new ApiResponse
        {
            StatusCode = statusCode,
            Body = JsonSerializer.Serialize(body, JsonOptions),
        };
    }

    public static ApiResponse Text(int statusCode, string body)
    {
        return new ApiResponse
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string> { ["Content-Type"] = "text/plain; charset=utf-8" },
            Body = body,
        };
    }

    public Stream ToStream()
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this, JsonOptions)));
    }
}
