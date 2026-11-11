using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Payment.Core.P24.Options;
using Payment.Sample.Api.Services;
using Swashbuckle.AspNetCore.Filters;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSingleton<NotificationStore>();
builder.Services.AddP24(builder.Configuration);
builder.Services.AddSingleton<IP24ProviderFactory, P24ProviderFactory>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.ExampleFilters();
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"));
});

builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

var app = builder.Build();

// The console creates payments and issues refunds with the configured P24 account. Outside Development it is
// therefore closed until TestConsole:Password is set, and then every request needs that password (HTTP Basic,
// any user name) – the page, Swagger and the API alike. POST /api/notify stays open: Przelewy24 calls it, and
// a notification is checked by its signature.
if (!app.Environment.IsDevelopment())
{
    var password = app.Configuration["TestConsole:Password"];

    app.Use(async (context, next) =>
    {
        if (HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.Equals("/api/notify", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("The test console is closed: set TestConsole:Password to open it.");
            return;
        }

        if (!HasPassword(context.Request.Headers.Authorization.ToString(), password))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"P24 test console\", charset=\"UTF-8\"";
            return;
        }

        await next();
    });
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();

// True when the Authorization header is "Basic base64(user:password)" with the expected password.
static bool HasPassword(string authorization, string password)
{
    const string scheme = "Basic ";
    if (!authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    try
    {
        var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[scheme.Length..].Trim()));
        var separator = credentials.IndexOf(':');
        return separator >= 0 && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(credentials[(separator + 1)..]), Encoding.UTF8.GetBytes(password));
    }
    catch (FormatException)
    {
        return false;
    }
}
