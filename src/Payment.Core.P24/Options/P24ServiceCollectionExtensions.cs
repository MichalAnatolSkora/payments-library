using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Payment.Core.P24.Providers;
using Payment.Models.Abstractions;

namespace Payment.Core.P24.Options;

public static class P24ServiceCollectionExtensions
{
    public const string SectionName = "P24";

    /// <summary>
    /// Registers <see cref="IPaymentProvider"/> and <see cref="IPaymentHistoryProvider"/> backed by Przelewy24,
    /// bound to the <c>P24</c> configuration section. Options are validated when the host starts.
    /// <see cref="P24Provider"/> itself is resolvable too, for P24-specific calls such as <c>TestAccessAsync</c>.
    /// If logging is registered, <see cref="P24Provider"/> logs failed calls and rejected notifications at Warning.
    /// </summary>
    public static IServiceCollection AddP24(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"Missing '{SectionName}' configuration section. Expected keys: MerchantId, PosId, ApiKey, CrcKey, IsSandbox.");
        }

        services.AddOptions<P24Options>()
            .Bind(section)
            .Validate(o => o.MerchantId > 0, $"{SectionName}:MerchantId must be a positive number.")
            .Validate(o => o.PosId > 0, $"{SectionName}:PosId must be a positive number.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), $"{SectionName}:ApiKey must be set.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.CrcKey), $"{SectionName}:CrcKey must be set.")
            .Validate(o => o.BaseAddress.IsAbsoluteUri, $"{SectionName}:BaseAddress must be an absolute URI.")
            .ValidateOnStart();

        // Expose the bound options directly so P24Provider (a typed HttpClient) and
        // consumers can take P24Options without going through IOptions<T>.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<P24Options>>().Value);

        services.AddHttpClient<P24Provider>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<P24Options>().BaseAddress;
        });

        // One typed client behind every abstraction the provider implements.
        services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<P24Provider>());
        services.AddTransient<IPaymentHistoryProvider>(sp => sp.GetRequiredService<P24Provider>());

        return services;
    }
}
