using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TripExpenseTracker.IntegrationTests.Support;

/// <summary>
/// Starts the API as Production with a temporary web root that holds a stub index.html (F1.T14). The stub is
/// written by the test setup, so no built Angular app and no committed app file is needed. The database is off,
/// as in <see cref="TestWebApplicationFactory"/>. The temporary folder is deleted when the factory is disposed.
/// </summary>
public sealed class StaticWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string StubIndexHtml = "<!doctype html><html><body><app-root>stub</app-root></body></html>";

    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "tet-webroot-" + Guid.NewGuid().ToString("N"));

    public StaticWebApplicationFactory()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), StubIndexHtml);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseWebRoot(_webRoot);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:MigrateOnStartup"] = "false",
                ["ConnectionStrings:Default"] = string.Empty,
                ["Auth:SessionCleanupEnabled"] = "false",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}
