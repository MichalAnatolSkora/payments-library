using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.Core.P24.Abstractions;
using Payment.Core.P24.Providers;

namespace Payment.Core.P24.Options;

public static class P24ServiceCollectionExtensions
{
    public static IServiceCollection AddP24(this IServiceCollection services, IConfiguration configuration)
    {
        var optionsSection = configuration.GetSection("P24")
            ?? throw new InvalidOperationException("Missing 'P24' configuration section.");
        var options = optionsSection.Get<P24Options>();
        services.Configure<P24Options>(optionsSection);

        services.AddSingleton<IP24ProviderFactory, P24ProviderFactory>();
        services.AddHttpClient<IPaymentProvider, P24Provider>(client =>
        {
            client.BaseAddress = new Uri(options!.IsSandbox
                ? "https://sandbox.przelewy24.pl"
                : "https://secure.przelewy24.pl");
        });

        return services;
    }
}
