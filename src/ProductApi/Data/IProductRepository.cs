using ProductApi.Models;

namespace ProductApi.Data;

public interface IProductRepository
{
    Task<Product?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    Task PutAsync(Product product, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
