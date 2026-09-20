namespace Payment.Tests;

/// <summary>
/// A fact that runs only when sandbox credentials are present next to the test binary.
/// Lets a plain <c>dotnet test</c> pass in CI while the integration suite stays runnable locally.
/// </summary>
public sealed class SandboxFactAttribute : FactAttribute
{
    public const string ConfigFile = "appsettings.Testing.json";

    public SandboxFactAttribute()
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, ConfigFile)))
        {
            Skip = $"{ConfigFile} with P24 sandbox credentials not found.";
        }
    }
}
