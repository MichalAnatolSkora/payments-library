using Clerk.Net.AspNetCore.Security;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services
    .AddAuthentication()
    .AddClerkAuthentication(options =>
    {
        // TODO: replace with your Clerk instance URL (e.g. https://your-app.clerk.accounts.dev)
        options.Authority = builder.Configuration["Clerk:Authority"]!;
        options.AuthorizedParty = builder.Configuration["Clerk:AuthorizedParty"]!;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

var spaPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "browser");

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(spaPath)
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(spaPath)
});

app.UseAuthentication();
app.UseAuthorization();

// Example protected API endpoint
app.MapGet("/api/me", (HttpContext ctx) =>
{
    var userId = ctx.User.FindFirst("sub")?.Value;
    return Results.Ok(new { userId });
}).RequireAuthorization();

app.MapFallback(async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(spaPath, "index.html"));
});

app.Run();
