using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProductApi.Functions;
using ProductApi.Models;

namespace ProductApi.Tests;

[TestClass]
public sealed class ProductFunctionTests
{
    [TestMethod]
    public void EventParserDecodesBase64AndMapsParameters()
    {
        using Stream input = TestSupport.Event(
            "PUT",
            "/products/p1",
            new Product("p1", "Tea", 12.34m),
            base64: true,
            pathParameters: new Dictionary<string, string> { ["id"] = "p1" },
            query: new Dictionary<string, string> { ["limit"] = "20" });

        ApiGatewayEvent request = ApiGatewayEvent.Parse(input);

        Assert.AreEqual("PUT", request.HttpMethod);
        Assert.AreEqual("p1", request.GetProductId());
        Assert.AreEqual("20", request.QueryStringParameters["limit"]);
        StringAssert.Contains(request.Body, "\"price\":12.34");
    }

    [TestMethod]
    public async Task CreateStoresProductAndReturns201()
    {
        var repository = new FakeProductRepository();
        using JsonDocument response = TestSupport.Response(new CreateProduct(repository).Handler(
            TestSupport.Event("POST", "/products", new Product("p1", "Tea", 12.34m)), null!));

        Assert.AreEqual(201, TestSupport.Status(response));
        Assert.AreEqual("Created product with id p1", TestSupport.Body(response));
        Assert.AreEqual("Tea", (await repository.GetAsync("p1"))?.Name);
    }

    [TestMethod]
    public async Task CreateGeneratesIdentifierWhenMissing()
    {
        var repository = new FakeProductRepository();
        using JsonDocument response = TestSupport.Response(new CreateProduct(repository).Handler(
            TestSupport.Event("POST", "/products", new Product("", "Coffee", 8m)), null!));

        Assert.AreEqual(201, TestSupport.Status(response));
        IReadOnlyList<Product> products = await repository.GetAllAsync();
        Assert.AreEqual(1, products.Count);
        Assert.IsTrue(Guid.TryParse(products[0].Id, out _));
    }

    [TestMethod]
    public void GetReturnsProductJson()
    {
        var repository = new FakeProductRepository();
        repository.PutAsync(new Product("p1", "Tea", 12.34m)).GetAwaiter().GetResult();
        using JsonDocument response = TestSupport.Response(new GetProduct(repository).Handler(
            TestSupport.Event("GET", "/products/p1", pathParameters: new Dictionary<string, string> { ["id"] = "p1" }), null!));

        Assert.AreEqual(200, TestSupport.Status(response));
        using JsonDocument body = JsonDocument.Parse(TestSupport.Body(response));
        Assert.AreEqual("p1", body.RootElement.GetProperty("id").GetString());
        Assert.AreEqual(12.34m, body.RootElement.GetProperty("price").GetDecimal());
    }

    [TestMethod]
    public void GetMissingProductReturns404()
    {
        using JsonDocument response = TestSupport.Response(new GetProduct(new FakeProductRepository()).Handler(
            TestSupport.Event("GET", "/products/missing", pathParameters: new Dictionary<string, string> { ["id"] = "missing" }), null!));

        Assert.AreEqual(404, TestSupport.Status(response));
        Assert.AreEqual("Not Found", TestSupport.Body(response));
    }

    [TestMethod]
    public void ListReturnsSourceWrapperShape()
    {
        var repository = new FakeProductRepository();
        repository.PutAsync(new Product("p2", "Coffee", 8m)).GetAwaiter().GetResult();
        repository.PutAsync(new Product("p1", "Tea", 12.34m)).GetAwaiter().GetResult();
        using JsonDocument response = TestSupport.Response(new GetProducts(repository).Handler(
            TestSupport.Event("GET", "/products"), null!));

        Assert.AreEqual(200, TestSupport.Status(response));
        using JsonDocument body = JsonDocument.Parse(TestSupport.Body(response));
        Assert.AreEqual(2, body.RootElement.GetProperty("products").GetArrayLength());
    }

    [TestMethod]
    public async Task UpdateDecodesBase64AndWritesWholeProduct()
    {
        var repository = new FakeProductRepository();
        using JsonDocument response = TestSupport.Response(new UpdateProduct(repository).Handler(
            TestSupport.Event(
                "PUT",
                "/products/p1",
                new Product("p1", "Updated", 99.99m),
                base64: true,
                pathParameters: new Dictionary<string, string> { ["id"] = "p1" }),
            null!));

        Assert.AreEqual(201, TestSupport.Status(response));
        Assert.AreEqual(99.99m, (await repository.GetAsync("p1"))?.Price);
    }

    [TestMethod]
    public async Task UpdateRejectsIdentifierMismatch()
    {
        var repository = new FakeProductRepository();
        using JsonDocument response = TestSupport.Response(new UpdateProduct(repository).Handler(
            TestSupport.Event(
                "PUT",
                "/products/p1",
                new Product("p2", "Wrong", 1m),
                pathParameters: new Dictionary<string, string> { ["id"] = "p1" }),
            null!));

        Assert.AreEqual(400, TestSupport.Status(response));
        Assert.AreEqual(0, (await repository.GetAllAsync()).Count);
    }

    [TestMethod]
    public async Task DeleteReadsThenDeletesProduct()
    {
        var repository = new FakeProductRepository();
        await repository.PutAsync(new Product("p1", "Tea", 12.34m));
        using JsonDocument response = TestSupport.Response(new DeleteProduct(repository).Handler(
            TestSupport.Event("DELETE", "/products/p1", pathParameters: new Dictionary<string, string> { ["id"] = "p1" }), null!));

        Assert.AreEqual(200, TestSupport.Status(response));
        Assert.IsNull(await repository.GetAsync("p1"));
    }

    [TestMethod]
    public void DeleteMissingProductReturns404()
    {
        using JsonDocument response = TestSupport.Response(new DeleteProduct(new FakeProductRepository()).Handler(
            TestSupport.Event("DELETE", "/products/missing", pathParameters: new Dictionary<string, string> { ["id"] = "missing" }), null!));

        Assert.AreEqual(404, TestSupport.Status(response));
    }

    [TestMethod]
    public void WrongMethodReturns405()
    {
        using JsonDocument response = TestSupport.Response(new GetProducts(new FakeProductRepository()).Handler(
            TestSupport.Event("POST", "/products"), null!));

        Assert.AreEqual(405, TestSupport.Status(response));
        Assert.IsFalse(response.RootElement.GetProperty("isBase64Encoded").GetBoolean());
    }
}
