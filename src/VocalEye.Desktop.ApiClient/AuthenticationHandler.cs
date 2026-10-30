using System.Net.Http.Headers;

namespace VocalEye.Desktop.ApiClient;

public sealed class AuthenticationHandler : DelegatingHandler
{
    private readonly string? _accessToken;

    public AuthenticationHandler(string? accessToken)
    {
        _accessToken = accessToken;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
