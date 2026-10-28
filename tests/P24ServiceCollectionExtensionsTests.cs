using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Payment.Models.Abstractions;
using Payment.Core.P24.Options;
using Payment.Core.P24.Providers;
using Payment.Models.Requests;

namespace Payment.Tests;

public sealed class P24ServiceCollectionExtensionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    private static IConfiguration ValidConfig(string sandbox = "true") => Config(
        ("P24:MerchantId", "12345"),
        ("P24:PosId", "12345"),
        ("P24:ApiKey", "api-key"),
        ("P24:CrcKey", "crc-key"),
        ("P24:IsSandbox", sandbox));

    [Fact]
    public void AddP24_MissingSection_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddP24(Config()));

        Assert.Contains("'P24'", ex.Message);
    }

    [Fact]
    public void AddP24_ValidSection_ResolvesProviderAndOptions()
    {
        var services = new ServiceCollection();
        services.AddP24(ValidConfig());
        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<P24Options>();
        var provider = sp.GetRequiredService<IPaymentProvider>();

        Assert.Equal(12345, options.MerchantId);
        Assert.True(options.IsSandbox);
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddP24_ResolvesHistoryProviderAndConcreteProvider()
    {
        var services = new ServiceCollection();
        services.AddP24(ValidConfig());
        using var sp = services.BuildServiceProvider();

        Assert.IsType<P24Provider>(sp.GetRequiredService<IPaymentProvider>());
        Assert.IsType<P24Provider>(sp.GetRequiredService<IPaymentHistoryProvider>());
        Assert.NotNull(sp.GetRequiredService<P24Provider>());
    }

    [Fact]
    public async Task AddP24_BothInterfacesSendThroughTheTypedHttpClient()
    {
        // Handlers configured on the "P24Provider" client apply to both interfaces. The base address
        // keeps any request that bypasses the stub on this machine instead of reaching P24.
        var handler = new StubHttpMessageHandler((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        var services = new ServiceCollection();
        services.AddP24(Config(
            ("P24:MerchantId", "12345"), ("P24:PosId", "12345"),
            ("P24:ApiKey", "api-key"), ("P24:CrcKey", "crc-key"),
            ("P24:BaseAddress", "http://127.0.0.1:9/")));
        services.AddHttpClient<P24Provider>().ConfigurePrimaryHttpMessageHandler(() => handler);
        using var sp = services.BuildServiceProvider();

        await sp.GetRequiredService<IPaymentProvider>().GetPaymentStatusAsync("order-1");
        await sp.GetRequiredService<IPaymentHistoryProvider>().GetPaymentHistoryAsync(new PaymentHistoryRequest
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 31),
        });

        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public void AddP24_MissingIsSandbox_DefaultsToSandbox()
    {
        // A config that forgets IsSandbox (or uses the old "Sandbox" key) must not hit production.
        var services = new ServiceCollection();
        services.AddP24(Config(
            ("P24:MerchantId", "12345"), ("P24:PosId", "12345"),
            ("P24:ApiKey", "api-key"), ("P24:CrcKey", "crc-key"),
            ("P24:Sandbox", "false")));
        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<P24Options>();
        Assert.True(options.IsSandbox);
        Assert.Equal(P24Options.SandboxBaseAddress, options.BaseAddress);
    }

    [Fact]
    public void AddP24_IsSandboxFalse_TargetsProduction()
    {
        var services = new ServiceCollection();
        services.AddP24(ValidConfig(sandbox: "false"));
        using var sp = services.BuildServiceProvider();

        Assert.Equal(P24Options.ProductionBaseAddress, sp.GetRequiredService<P24Options>().BaseAddress);
    }

    [Fact]
    public void AddP24_ExplicitBaseAddress_OverridesSandboxSwitch()
    {
        var services = new ServiceCollection();
        services.AddP24(Config(
            ("P24:MerchantId", "12345"), ("P24:PosId", "12345"),
            ("P24:ApiKey", "api-key"), ("P24:CrcKey", "crc-key"),
            ("P24:IsSandbox", "false"),
            ("P24:BaseAddress", "http://localhost:8089/")));
        using var sp = services.BuildServiceProvider();

        Assert.Equal(new Uri("http://localhost:8089/"), sp.GetRequiredService<P24Options>().BaseAddress);
    }

    [Fact]
    public void AddP24_RelativeBaseAddress_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddP24(Config(
            ("P24:MerchantId", "12345"), ("P24:PosId", "12345"),
            ("P24:ApiKey", "api-key"), ("P24:CrcKey", "crc-key"),
            ("P24:BaseAddress", "/relative")));
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<P24Options>>().Value);
        Assert.Contains("BaseAddress", ex.Message);
    }

    [Fact]
    public void AddP24_WithoutLogging_ProviderStillResolves()
    {
        // ILogger<P24Provider> is optional; no logging registration must not break construction.
        var services = new ServiceCollection();
        services.AddP24(ValidConfig());
        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IPaymentProvider>());
    }

    [Theory]
    [InlineData("P24:MerchantId", "0", "MerchantId")]
    [InlineData("P24:PosId", "0", "PosId")]
    [InlineData("P24:ApiKey", "", "ApiKey")]
    [InlineData("P24:CrcKey", " ", "CrcKey")]
    public void AddP24_InvalidValue_FailsValidation(string key, string value, string expectedInMessage)
    {
        var services = new ServiceCollection();
        var pairs = new List<(string, string)>
        {
            ("P24:MerchantId", "12345"), ("P24:PosId", "12345"),
            ("P24:ApiKey", "api-key"), ("P24:CrcKey", "crc-key"),
        };
        pairs.RemoveAll(p => p.Item1 == key);
        pairs.Add((key, value));
        services.AddP24(Config(pairs.ToArray()));
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<P24Options>>().Value);

        Assert.Contains(expectedInMessage, ex.Message);
    }
}
