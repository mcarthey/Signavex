using System.Net;
using System.Text;

namespace Signavex.Infrastructure.Tests.Helpers;

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseContent;

    public HttpRequestMessage? LastRequest { get; private set; }

    public MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseContent = responseContent;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
        });
    }
}
