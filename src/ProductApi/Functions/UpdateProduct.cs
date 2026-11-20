using HC.Serverless.Function.Common;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Functions;

public sealed class UpdateProduct : ProductFunctionBase
{
    public UpdateProduct()
    {
    }

    public UpdateProduct(IProductRepository repository)
        : base(repository)
    {
    }

    public Stream Handler(Stream input, IFunctionContext context)
    {
        return Run(input, context, "PUT", HandleAsync);
    }

    private async Task<ApiResponse> HandleAsync(ApiGatewayEvent request)
    {
        string id = request.GetProductId();
        Product? product = ReadProduct(request);
        if (product is null || string.IsNullOrWhiteSpace(id) || !string.Equals(id, product.Id, StringComparison.Ordinal))
        {
            return ApiResponse.Text(400, "Product ID in the body does not match path parameter");
        }

        await Repository.PutAsync(product);
        return ApiResponse.Text(201, "Created product with id " + id);
    }
}
