using System.Net;
using System.Text;

namespace Signavex.Infrastructure.Tests.Helpers;

internal class SequentialMockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(string Content, HttpStatusCode Status)> _responses;

    public List<HttpRequestMessage> Requests { get; } = new();

    public SequentialMockHttpMessageHandler(params (string Content, HttpStatusCode Status)[] responses)
    {
        _responses = new Queue<(string, HttpStatusCode)>(responses);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (_responses.Count == 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("No more responses configured", Encoding.UTF8, "text/plain")
            });
        }

        var (content, status) = _responses.Dequeue();
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });
    }
}
