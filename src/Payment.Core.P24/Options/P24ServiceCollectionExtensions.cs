using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.Core.P24.Abstractions;
using Payment.Core.P24.Providers.Przelewy24;

namespace Payment.Core.P24.Options;

public static class P24ServiceCollectionExtensions
{
    public static IServiceCollection AddP24(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("P24").Get<P24Options>()
            ?? throw new InvalidOperationException("Missing 'P24' configuration section.");

        services.AddSingleton(options);

        services.AddHttpClient();
        services.AddSingleton<IP24ProviderFactory, P24ProviderFactory>();
        services.AddHttpClient<IPaymentProvider, P24Provider>(client =>
        {
            client.BaseAddress = new Uri(options.IsSandbox
                ? "https://sandbox.przelewy24.pl"
                : "https://secure.przelewy24.pl");
        });

        return services;
    }
}
