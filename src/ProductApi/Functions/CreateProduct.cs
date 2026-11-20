using HC.Serverless.Function.Common;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Functions;

public sealed class CreateProduct : ProductFunctionBase
{
    public CreateProduct()
    {
    }

    public CreateProduct(IProductRepository repository)
        : base(repository)
    {
    }

    public Stream Handler(Stream input, IFunctionContext context)
    {
        return Run(input, context, "POST", HandleAsync);
    }

    private async Task<ApiResponse> HandleAsync(ApiGatewayEvent request)
    {
        Product? product = ReadProduct(request);
        if (product is null)
        {
            return ApiResponse.Text(400, "Product body is required");
        }
        if (string.IsNullOrWhiteSpace(product.Id))
        {
            product.Id = Guid.NewGuid().ToString("D");
        }

        await Repository.PutAsync(product);
        return ApiResponse.Text(201, "Created product with id " + product.Id);
    }
}
