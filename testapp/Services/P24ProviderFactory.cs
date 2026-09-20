using Payment.Core.P24.Options;
using Payment.Core.P24.Providers;
using Payment.Sample.Api.Handlers;

namespace Payment.Sample.Api.Services;

/// <summary>
/// Sample-only factory so the "-raw" endpoints can capture the wire traffic.
/// Production code should inject <c>IPaymentProvider</c> registered by <c>AddP24</c>.
/// </summary>
public interface IP24ProviderFactory
{
    P24Provider Create();
    (P24Provider Provider, CapturingHandler Handler) CreateWithCapture();
}

public sealed class P24ProviderFactory(IHttpClientFactory httpClientFactory, P24Options options) : IP24ProviderFactory
{
    public P24Provider Create() => new(options, httpClientFactory.CreateClient());

    public (P24Provider Provider, CapturingHandler Handler) CreateWithCapture()
    {
        var handler = new CapturingHandler();
        return (new P24Provider(options, new HttpClient(handler)), handler);
    }
}
