using System.Text.Json.Serialization;

namespace ProductApi.Models;

public sealed class Product
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    public Product()
    {
    }

    public Product(string id, string name, decimal price)
    {
        Id = id;
        Name = name;
        Price = price;
    }
}
