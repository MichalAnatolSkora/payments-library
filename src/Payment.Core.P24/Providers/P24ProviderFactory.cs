using Payment.Core.P24.Abstractions;
using Payment.Core.P24.Handlers;
using Payment.Core.P24.Options;

namespace Payment.Core.P24.Providers;

public class P24ProviderFactory : IP24ProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public P24ProviderFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public P24Provider Create(P24Options options)
    {
        var client = _httpClientFactory.CreateClient();
        return new P24Provider(options, client);
    }

    public (P24Provider Provider, CapturingHandler Handler) CreateWithCapture(P24Options options)
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler);
        var provider = new P24Provider(options, client);
        return (provider, handler);
    }
}
