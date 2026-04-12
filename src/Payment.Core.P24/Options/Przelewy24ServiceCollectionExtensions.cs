using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.Infrastructure.P24.Abstractions;
using Payment.Infrastructure.P24.Providers.Przelewy24;

namespace Payment.Infrastructure.P24.Options;

public static class Przelewy24ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="Przelewy24Provider"/> as <see cref="IPaymentProvider"/>
    /// using the "Przelewy24" section from appsettings.json.
    ///
    /// Expected config section:
    /// <code>
    /// "Przelewy24": {
    ///   "MerchantId": 0,
    ///   "PosId":      0,
    ///   "ApiKey":     "",
    ///   "CrcKey":     "",
    ///   "Sandbox":    true
    /// }
    /// </code>
    /// </summary>
    public static IServiceCollection AddPrzelewy24(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("Przelewy24")
            .Get<P24Options>()
            ?? throw new InvalidOperationException(
                "Missing 'Przelewy24' configuration section.");

        services.AddHttpClient<IPaymentProvider, Przelewy24Provider>(client =>
        {
            client.BaseAddress = new Uri(options.IsSandbox
                ? "https://sandbox.przelewy24.pl"
                : "https://secure.przelewy24.pl");
        });

        services.AddSingleton(options);

        return services;
    }
}
