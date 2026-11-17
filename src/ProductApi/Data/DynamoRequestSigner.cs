using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ProductApi.Data;

public sealed class SignedDynamoRequest
{
    public required string Authorization { get; init; }
    public required string Timestamp { get; init; }
    public required string PayloadHash { get; init; }
    public required string Target { get; init; }
    public required string SignedHeaders { get; init; }
}

public static class DynamoRequestSigner
{
    private const string Service = "dynamodb";
    private const string ContentType = "application/x-amz-json-1.0";

    public static SignedDynamoRequest Sign(
        Uri endpoint,
        string region,
        string accessKey,
        string secretKey,
        string operation,
        ReadOnlySpan<byte> body,
        DateTimeOffset timestamp)
    {
        string date = timestamp.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string dateTime = timestamp.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string payloadHash = Hex(SHA256.HashData(body));
        string host = endpoint.IsDefaultPort ? endpoint.Host : endpoint.Authority;
        string target = "DynamoDB_20120810." + operation;
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["content-type"] = ContentType,
            ["host"] = host,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = dateTime,
            ["x-amz-target"] = target,
        };
        string canonicalHeaders = string.Concat(headers.Select(pair => pair.Key + ":" + pair.Value.Trim() + "\n"));
        string signedHeaders = string.Join(";", headers.Keys);
        string canonicalUri = CanonicalUri(endpoint.AbsolutePath);
        string canonicalRequest = string.Join("\n", new[]
        {
            "POST",
            canonicalUri,
            string.Empty,
            canonicalHeaders,
            signedHeaders,
            payloadHash,
        });
        string scope = date + "/" + region + "/" + Service + "/aws4_request";
        string stringToSign = string.Join("\n", new[]
        {
            "AWS4-HMAC-SHA256",
            dateTime,
            scope,
            Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))),
        });
        byte[] signingKey = Sign(Sign(Sign(Sign(
            Encoding.UTF8.GetBytes("AWS4" + secretKey), date), region), Service), "aws4_request");
        string signature = Hex(Sign(signingKey, stringToSign));

        return new SignedDynamoRequest
        {
            Authorization = "AWS4-HMAC-SHA256 Credential=" + accessKey + "/" + scope
                + ", SignedHeaders=" + signedHeaders + ", Signature=" + signature,
            Timestamp = dateTime,
            PayloadHash = payloadHash,
            Target = target,
            SignedHeaders = signedHeaders,
        };
    }

    private static string CanonicalUri(string path)
    {
        string normalized = string.IsNullOrEmpty(path) ? "/" : path;
        string encoded = string.Join("/", normalized.Split('/').Select(Uri.EscapeDataString));
        if (!encoded.StartsWith("/", StringComparison.Ordinal))
        {
            encoded = "/" + encoded;
        }
        return encoded.EndsWith("/", StringComparison.Ordinal) ? encoded : encoded + "/";
    }

    private static byte[] Sign(byte[] key, string value)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static string Hex(ReadOnlySpan<byte> value)
    {
        return Convert.ToHexString(value).ToLowerInvariant();
    }
}
