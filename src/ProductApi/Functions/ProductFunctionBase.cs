using System.Text.Json;
using HC.Serverless.Function.Common;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Functions;

public abstract class ProductFunctionBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    protected IProductRepository Repository { get; }

    protected ProductFunctionBase()
        : this(new GeminiProductRepository())
    {
    }

    protected ProductFunctionBase(IProductRepository repository)
    {
        Repository = repository;
    }

    protected Stream Run(
        Stream input,
        IFunctionContext context,
        string expectedMethod,
        Func<ApiGatewayEvent, Task<ApiResponse>> action)
    {
        try
        {
            ApiGatewayEvent request = ApiGatewayEvent.Parse(input);
            if (!string.Equals(request.HttpMethod, expectedMethod, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse.Text(405, "Only " + expectedMethod + " allowed").ToStream();
            }

            Log(context, "Received " + request.HttpMethod + " " + request.Path);
            return action(request).GetAwaiter().GetResult().ToStream();
        }
        catch (JsonException exception)
        {
            Log(context, "Invalid JSON request: " + exception.Message);
            return ApiResponse.Text(400, "Invalid JSON body").ToStream();
        }
        catch (FormatException exception)
        {
            Log(context, "Invalid encoded request: " + exception.Message);
            return ApiResponse.Text(400, "Invalid encoded body").ToStream();
        }
        catch (Exception exception)
        {
            Log(context, "Product operation failed: " + exception.Message + " " + exception.StackTrace);
            return ApiResponse.Text(500, "Not Found").ToStream();
        }
    }

    protected static Product? ReadProduct(ApiGatewayEvent request)
    {
        return string.IsNullOrWhiteSpace(request.Body)
            ? null
            : JsonSerializer.Deserialize<Product>(request.Body, JsonOptions);
    }

    protected static void Log(IFunctionContext context, string message)
    {
        string requestId = context?.RequestId ?? "unknown";
        string functionName = context?.FunctionName ?? "unknown";
        string line = "[PRODUCT-API] requestId=" + requestId + " function=" + functionName + " " + message;
        try
        {
            context?.Logger?.Log(line);
        }
        catch (Exception)
        {
            // Standard output remains available when the context logger is unavailable.
        }
        Console.WriteLine(line);
    }
}
