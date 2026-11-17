using HC.Serverless.Function.Common;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Functions;

public sealed class GetProduct : ProductFunctionBase
{
    public GetProduct()
    {
    }

    public GetProduct(IProductRepository repository)
        : base(repository)
    {
    }

    public Stream Handler(Stream input, IFunctionContext context)
    {
        return Run(input, context, "GET", HandleAsync);
    }

    private async Task<ApiResponse> HandleAsync(ApiGatewayEvent request)
    {
        string id = request.GetProductId();
        Product? product = string.IsNullOrWhiteSpace(id) ? null : await Repository.GetAsync(id);
        return product is null
            ? ApiResponse.Text(404, "Not Found")
            : ApiResponse.Json(200, product);
    }
}
