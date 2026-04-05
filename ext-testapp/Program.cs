using Clerk.Net.AspNetCore.Security;
using ExtTestApp.Handlers;
using ExtTestApp.Services;
using Microsoft.Extensions.FileProviders;
using Payment.Infrastructure.P24.Abstractions;
using Payment.Infrastructure.P24.Options;
using Payment.Infrastructure.P24.Providers.Przelewy24;
using Payment.Models.Requests;
using Swashbuckle.AspNetCore.Filters;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.Services
    .AddAuthentication()
    .AddClerkAuthentication(options =>
    {
        options.Authority = builder.Configuration["Clerk:Authority"]!;
        options.AuthorizedParty = builder.Configuration["Clerk:AuthorizedParty"]!;
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<NotificationStore>();
builder.Services.AddPrzelewy24(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ExtTestApp API", Version = "v1" });
    c.ExampleFilters();
});
builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

var spaPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "browser");

app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(spaPath) });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(spaPath) });

app.UseAuthentication();
app.UseAuthorization();

// --- raw capture helper (debug only, bypasses DI) ---

static (Przelewy24Provider provider, CapturingHandler handler) ProviderWithCapture(Przelewy24Options options)
{
    var capturing = new CapturingHandler();
    return (new Przelewy24Provider(options, new HttpClient(capturing)), capturing);
}

// --- API endpoints ---

app.MapGet("/api/me", (HttpContext ctx) =>
{
    var userId = ctx.User.FindFirst("sub")?.Value;
    return Results.Ok(new { userId });
}).RequireAuthorization();

app.MapGet("/api/config", (Przelewy24Options options) =>
{
    return Results.Ok(new
    {
        options.MerchantId,
        options.PosId,
        options.ApiKey,
        options.CrcKey,
        options.Sandbox,
    });
});

app.MapPost("/api/create-payment", async (CreatePaymentRequest body, IPaymentProvider provider) =>
{
    return Results.Ok(await provider.CreatePaymentAsync(body));
});

app.MapPost("/api/create-payment-raw", async (CreatePaymentRequest body, Przelewy24Options options) =>
{
    var (provider, handler) = ProviderWithCapture(options);
    await provider.CreatePaymentAsync(body);
    return Results.Ok(handler.Capture());
});

app.MapGet("/api/payment-status/{sessionId}", async (string sessionId, IPaymentProvider provider) =>
{
    return Results.Ok(await provider.GetPaymentStatusAsync(sessionId));
});

app.MapGet("/api/payment-status-raw/{sessionId}", async (string sessionId, Przelewy24Options options) =>
{
    var (provider, handler) = ProviderWithCapture(options);
    await provider.GetPaymentStatusAsync(sessionId);
    return Results.Ok(handler.Capture());
});

app.MapPost("/api/confirm-payment", async (ConfirmPaymentRequest body, IPaymentProvider provider) =>
{
    return Results.Ok(await provider.ConfirmPaymentAsync(body));
});

app.MapPost("/api/confirm-payment-raw", async (ConfirmPaymentRequest body, Przelewy24Options options) =>
{
    var (provider, handler) = ProviderWithCapture(options);
    await provider.ConfirmPaymentAsync(body);
    return Results.Ok(handler.Capture());
});

app.MapPost("/api/refund", async (RefundRequest body, IPaymentProvider provider) =>
{
    return Results.Ok(await provider.RefundAsync(body));
});

app.MapPost("/api/refund-raw", async (RefundRequest body, Przelewy24Options options) =>
{
    var (provider, handler) = ProviderWithCapture(options);
    await provider.RefundAsync(body);
    return Results.Ok(handler.Capture());
});

app.MapPost("/api/notify", async (HttpContext ctx, IPaymentProvider provider, NotificationStore store, ILogger<Program> logger) =>
{
    // P24 REST API sends JSON, legacy API sends form-urlencoded — handle both
    InstantPaymentNotificationRequest payload;
    var contentType = ctx.Request.ContentType ?? "";

    if (contentType.Contains("application/json"))
    {
        payload = (await ctx.Request.ReadFromJsonAsync<InstantPaymentNotificationRequest>())!;
    }
    else
    {
        var form = await ctx.Request.ReadFormAsync();
        payload = new InstantPaymentNotificationRequest
        {
            MerchantId = int.Parse(form["merchantId"].ToString()),
            PosId = int.Parse(form["posId"].ToString()),
            SessionId = form["sessionId"].ToString(),
            Amount = int.Parse(form["amount"].ToString()),
            OriginAmount = int.Parse(form["originAmount"].ToString()),
            Currency = form["currency"].ToString(),
            OrderId = long.Parse(form["orderId"].ToString()),
            MethodId = int.Parse(form["methodId"].ToString()),
            Statement = form["statement"].ToString(),
            Sign = form["sign"].ToString(),
        };
    }

    logger.LogInformation("Received P24 notification for SessionId: {SessionId}", payload.SessionId);

    var notification = new PaymentNotification
    {
        SessionId = payload.SessionId,
        ProviderId = payload.OrderId.ToString(),
        Amount = payload.Amount,
        Currency = payload.Currency,
        Sign = payload.Sign,
        RawFields = new Dictionary<string, string>
        {
            ["merchantId"] = payload.MerchantId.ToString(),
            ["posId"] = payload.PosId.ToString(),
            ["originAmount"] = payload.OriginAmount.ToString(),
            ["methodId"] = payload.MethodId.ToString(),
            ["statement"] = payload.Statement ?? "",
        },
    };

    var valid = provider.ValidateNotification(notification);
    logger.LogInformation("P24 notification validation: {Valid} (SessionId: {SessionId})", valid, payload.SessionId);
    store.Add(payload, valid);

    return Results.Ok();
}).DisableAntiforgery();

app.MapGet("/api/notifications", (NotificationStore store) => Results.Ok(store.GetAll()));

app.MapFallback(async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(spaPath, "index.html"));
});

app.Run();
