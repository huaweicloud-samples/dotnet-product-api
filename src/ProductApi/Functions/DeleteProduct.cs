using HC.Serverless.Function.Common;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Functions;

public sealed class DeleteProduct : ProductFunctionBase
{
    public DeleteProduct()
    {
    }

    public DeleteProduct(IProductRepository repository)
        : base(repository)
    {
    }

    public Stream Handler(Stream input, IFunctionContext context)
    {
        return Run(input, context, "DELETE", HandleAsync);
    }

    private async Task<ApiResponse> HandleAsync(ApiGatewayEvent request)
    {
        string id = request.GetProductId();
        Product? product = string.IsNullOrWhiteSpace(id) ? null : await Repository.GetAsync(id);
        if (product is null)
        {
            return ApiResponse.Text(404, "Not Found");
        }

        await Repository.DeleteAsync(product.Id);
        return ApiResponse.Text(200, "Product with id " + id + " deleted");
    }
}
