using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ProductApi.Models;

namespace ProductApi.Data;

public sealed class GeminiProductRepository : IProductRepository
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    private readonly GeminiSettings _settings;
    private readonly HttpClient _client;

    public GeminiProductRepository()
        : this(GeminiSettings.FromEnvironment(), SharedClient)
    {
    }

    public GeminiProductRepository(GeminiSettings settings, HttpClient client)
    {
        _settings = settings;
        _client = client;
    }

    public async Task<Product?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        using JsonDocument response = await CallAsync("GetItem", new Dictionary<string, object>
        {
            ["TableName"] = _settings.TableName,
            ["Key"] = Key(id),
        }, cancellationToken);

        return response.RootElement.TryGetProperty("Item", out JsonElement item)
            && item.ValueKind == JsonValueKind.Object
            && item.EnumerateObject().Any()
                ? FromItem(item)
                : null;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using JsonDocument response = await CallAsync("Scan", new Dictionary<string, object>
        {
            ["TableName"] = _settings.TableName,
            ["Limit"] = 20,
        }, cancellationToken);

        var products = new List<Product>();
        if (response.RootElement.TryGetProperty("Items", out JsonElement items)
            && items.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                products.Add(FromItem(item));
            }
        }
        return products;
    }

    public async Task PutAsync(Product product, CancellationToken cancellationToken = default)
    {
        using JsonDocument response = await CallAsync("PutItem", new Dictionary<string, object>
        {
            ["TableName"] = _settings.TableName,
            ["Item"] = new Dictionary<string, object>
            {
                ["id"] = StringAttribute(product.Id),
                ["name"] = StringAttribute(product.Name),
                ["price"] = NumberAttribute(product.Price.ToString(CultureInfo.InvariantCulture)),
            },
        }, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using JsonDocument response = await CallAsync("DeleteItem", new Dictionary<string, object>
        {
            ["TableName"] = _settings.TableName,
            ["Key"] = Key(id),
        }, cancellationToken);
    }

    private async Task<JsonDocument> CallAsync(
        string operation,
        IReadOnlyDictionary<string, object> payload,
        CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload);
        SignedDynamoRequest signed = DynamoRequestSigner.Sign(
            _settings.Endpoint,
            _settings.Region,
            _settings.AccessKey,
            _settings.SecretKey,
            operation,
            body,
            DateTimeOffset.UtcNow);

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint);
        request.Headers.Host = _settings.Endpoint.IsDefaultPort
            ? _settings.Endpoint.Host
            : _settings.Endpoint.Authority;
        request.Headers.TryAddWithoutValidation("X-Amz-Date", signed.Timestamp);
        request.Headers.TryAddWithoutValidation("X-Amz-Content-SHA256", signed.PayloadHash);
        request.Headers.TryAddWithoutValidation("X-Amz-Target", signed.Target);
        request.Headers.TryAddWithoutValidation("Authorization", signed.Authorization);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-amz-json-1.0");

        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        string text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string code = "HttpError";
            string message = string.Empty;
            try
            {
                using JsonDocument error = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                JsonElement root = error.RootElement;
                code = ReadString(root, "__type", "code", "Code").Split('#').Last();
                message = ReadString(root, "message", "Message");
            }
            catch (JsonException)
            {
                message = text.Length > 300 ? text[..300] : text;
            }
            throw new GeminiException(operation, (int)response.StatusCode, code, message);
        }

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
    }

    private static Dictionary<string, object> Key(string id)
    {
        return new Dictionary<string, object> { ["id"] = StringAttribute(id) };
    }

    private static Dictionary<string, string> StringAttribute(string value)
    {
        return new Dictionary<string, string> { ["S"] = value };
    }

    private static Dictionary<string, string> NumberAttribute(string value)
    {
        return new Dictionary<string, string> { ["N"] = value };
    }

    private static Product FromItem(JsonElement item)
    {
        string id = ReadAttribute(item, "id", "S");
        string name = ReadAttribute(item, "name", "S");
        string price = ReadAttribute(item, "price", "N");
        if (!decimal.TryParse(price, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
        {
            throw new InvalidDataException("Product price is not a valid number");
        }
        return new Product(id, name, amount);
    }

    private static string ReadAttribute(JsonElement item, string name, string type)
    {
        if (item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty(name, out JsonElement attribute)
            && attribute.ValueKind == JsonValueKind.Object
            && attribute.TryGetProperty(type, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }
        throw new InvalidDataException("Product item is missing " + name);
    }

    private static string ReadString(JsonElement root, params string[] names)
    {
        foreach (string name in names)
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }
}
