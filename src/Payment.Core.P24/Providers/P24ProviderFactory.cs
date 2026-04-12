using Microsoft.Extensions.Options;
using Payment.Core.P24.Abstractions;
using Payment.Core.P24.Handlers;
using Payment.Core.P24.Options;

namespace Payment.Core.P24.Providers;

public class P24ProviderFactory : IP24ProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly P24Options _options;

    public P24ProviderFactory(
        IHttpClientFactory httpClientFactory,
        IOptions<P24Options> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public P24Provider Create()
    {
        var client = _httpClientFactory.CreateClient();
        return new P24Provider(_options, client);
    }

    public (P24Provider Provider, CapturingHandler Handler) CreateWithCapture()
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler);
        var provider = new P24Provider(_options, client);
        return (provider, handler);
    }
}
