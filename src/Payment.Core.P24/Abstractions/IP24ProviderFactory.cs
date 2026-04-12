using Payment.Core.P24.Handlers;
using Payment.Core.P24.Options;
using Payment.Core.P24.Providers.Przelewy24;

namespace Payment.Core.P24.Abstractions;

public interface IP24ProviderFactory
{
    P24Provider Create(P24Options options);
    (P24Provider Provider, CapturingHandler Handler) CreateWithCapture(P24Options options);
}
