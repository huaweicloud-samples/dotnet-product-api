using System.Text.Json.Serialization;

namespace ProductApi.Models;

public sealed class ProductList
{
    [JsonPropertyName("products")]
    public IReadOnlyList<Product> Products { get; }

    public ProductList(IReadOnlyList<Product> products)
    {
        Products = products;
    }
}
