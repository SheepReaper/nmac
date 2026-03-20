using Microsoft.Extensions.Options;

namespace NMAC.Videos.YTRestClient;

public class AuthHeaderHandler(IOptions<YTClientOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("x-goog-api-key", options.Value.ApiKey);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
