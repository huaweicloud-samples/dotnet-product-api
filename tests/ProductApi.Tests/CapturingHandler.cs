using System.Net;

namespace ProductApi.Tests;

internal sealed class CapturingHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;

    public CapturingHandler(params string[] responseBodies)
    {
        _responses = new Queue<HttpResponseMessage>(responseBodies.Select(body => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        }));
    }

    public List<CapturedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(new CapturedRequest
        {
            Target = request.Headers.GetValues("X-Amz-Target").Single(),
            Authorization = request.Headers.Authorization?.ToString()
                ?? request.Headers.GetValues("Authorization").Single(),
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken),
        });
        return _responses.Dequeue();
    }
}

internal sealed class CapturedRequest
{
    public required string Target { get; init; }
    public required string Authorization { get; init; }
    public required string Body { get; init; }
}
