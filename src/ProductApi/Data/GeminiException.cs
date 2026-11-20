namespace ProductApi.Data;

public sealed class GeminiException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public GeminiException(string operation, int statusCode, string errorCode, string message)
        : base(operation + " failed with HTTP " + statusCode + ": " + errorCode + " " + message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
