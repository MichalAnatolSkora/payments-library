using Payment.Core.P24.Handlers;
using Payment.Core.P24.Providers;

namespace Payment.Core.P24.Abstractions;

public interface IP24ProviderFactory
{
    P24Provider Create();
    (P24Provider Provider, CapturingHandler Handler) CreateWithCapture();
}
