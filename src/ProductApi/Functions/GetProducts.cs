using HC.Serverless.Function.Common;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Functions;

public sealed class GetProducts : ProductFunctionBase
{
    public GetProducts()
    {
    }

    public GetProducts(IProductRepository repository)
        : base(repository)
    {
    }

    public Stream Handler(Stream input, IFunctionContext context)
    {
        return Run(input, context, "GET", HandleAsync);
    }

    private async Task<ApiResponse> HandleAsync(ApiGatewayEvent request)
    {
        IReadOnlyList<Product> products = await Repository.GetAllAsync();
        return ApiResponse.Json(200, new ProductList(products));
    }
}
