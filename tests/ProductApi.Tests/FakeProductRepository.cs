using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Tests;

internal sealed class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<string, Product> _products = new(StringComparer.Ordinal);

    public Task<Product?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _products.TryGetValue(id, out Product? product);
        return Task.FromResult(product is null ? null : Clone(product));
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Product> products = _products.Values
            .OrderBy(product => product.Id, StringComparer.Ordinal)
            .Select(Clone)
            .ToList();
        return Task.FromResult(products);
    }

    public Task PutAsync(Product product, CancellationToken cancellationToken = default)
    {
        _products[product.Id] = Clone(product);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _products.Remove(id);
        return Task.CompletedTask;
    }

    private static Product Clone(Product product)
    {
        return new Product(product.Id, product.Name, product.Price);
    }
}
