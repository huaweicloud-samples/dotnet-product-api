using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Tests;

[TestClass]
public sealed class GeminiClientTests
{
    [TestMethod]
    public async Task RepositoryUsesOnlySingleItemAndScanOperations()
    {
        var handler = new CapturingHandler(
            "{\"Item\":{\"id\":{\"S\":\"p1\"},\"name\":{\"S\":\"Tea\"},\"price\":{\"N\":\"12.34\"}}}",
            "{\"Items\":[{\"id\":{\"S\":\"p1\"},\"name\":{\"S\":\"Tea\"},\"price\":{\"N\":\"12.34\"}}]}",
            "{}",
            "{}");
        var repository = new GeminiProductRepository(Settings(), new HttpClient(handler));

        Product? product = await repository.GetAsync("p1");
        IReadOnlyList<Product> products = await repository.GetAllAsync();
        await repository.PutAsync(new Product("p1", "Tea", 12.34m));
        await repository.DeleteAsync("p1");

        Assert.AreEqual("Tea", product?.Name);
        Assert.AreEqual(1, products.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                "DynamoDB_20120810.GetItem",
                "DynamoDB_20120810.Scan",
                "DynamoDB_20120810.PutItem",
                "DynamoDB_20120810.DeleteItem",
            },
            handler.Requests.Select(request => request.Target).ToArray());
        Assert.IsTrue(handler.Requests.All(request => request.Authorization.StartsWith("AWS4-HMAC-SHA256 Credential=AKID/", StringComparison.Ordinal)));
        Assert.IsTrue(handler.Requests.All(request => !request.Target.Contains("BatchWriteItem", StringComparison.Ordinal)));
        StringAssert.Contains(handler.Requests[1].Body, "\"Limit\":20");
        StringAssert.Contains(handler.Requests[2].Body, "\"price\":{\"N\":\"12.34\"}");
    }

    [TestMethod]
    public void SignerProducesDeterministicCanonicalHeaders()
    {
        byte[] body = Encoding.UTF8.GetBytes("{}");
        DateTimeOffset timestamp = new(2026, 8, 25, 8, 0, 0, TimeSpan.Zero);

        SignedDynamoRequest first = DynamoRequestSigner.Sign(
            new Uri("http://example.test:8000/"), "cn-north-4", "AKID", "SECRET", "GetItem", body, timestamp);
        SignedDynamoRequest second = DynamoRequestSigner.Sign(
            new Uri("http://example.test:8000/"), "cn-north-4", "AKID", "SECRET", "GetItem", body, timestamp);

        Assert.AreEqual("20260825T080000Z", first.Timestamp);
        Assert.AreEqual("44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a", first.PayloadHash);
        Assert.AreEqual("content-type;host;x-amz-content-sha256;x-amz-date;x-amz-target", first.SignedHeaders);
        Assert.AreEqual("DynamoDB_20120810.GetItem", first.Target);
        Assert.AreEqual(first.Authorization, second.Authorization);
        StringAssert.Contains(first.Authorization, "Credential=AKID/20260825/cn-north-4/dynamodb/aws4_request");
        StringAssert.Contains(first.Authorization, "Signature=");
    }

    private static GeminiSettings Settings()
    {
        return new GeminiSettings
        {
            Endpoint = new Uri("http://example.test:8000/"),
            Region = "cn-north-4",
            AccessKey = "AKID",
            SecretKey = "SECRET",
            TableName = "products",
        };
    }
}
