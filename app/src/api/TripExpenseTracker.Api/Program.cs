using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TripExpenseTracker.Api.Auth;
using TripExpenseTracker.Api.Logging;
using TripExpenseTracker.Api.Middleware;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Data;

var builder = WebApplication.CreateBuilder(args);

// Serilog is the only logging output. The console output is a Serilog sink, not a direct write.
// SensitiveDataRedactor masks secret-named properties (F1.R11) before any sink sees them.
// Extra sinks registered in DI are added too; tests use this to capture what the real pipeline logs.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With(new SensitiveDataRedactor())
        .WriteTo.Console();

    foreach (var sink in services.GetServices<ILogEventSink>())
    {
        configuration.WriteTo.Sink(sink);
    }
});

// Data access. The unit of work is scoped, so one request (or test scope) shares one connection and transaction.
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new NpgsqlConnectionFactory(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserDao, UserDao>();
builder.Services.AddScoped<ISessionDao, SessionDao>();

// Auth. AuthTransaction shares the scoped unit of work with the DAOs, so register and its session insert share one transaction (F1.R7).
builder.Services.AddScoped<IAuthTransaction>(sp =>
    new AuthTransaction(sp.GetRequiredService<IUnitOfWork>(), Log.ForContext<AuthTransaction>()));
builder.Services.AddOptions<AuthOptions>().Bind(builder.Configuration.GetSection("Auth"));
builder.Services.AddScoped(sp => sp.GetRequiredService<IOptions<AuthOptions>>().Value);
// One hasher for the app, so AuthService's dummy-hash cache (keyed by hasher instance) is shared across requests.
builder.Services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
builder.Services.AddScoped<AuthService>();
builder.Services.TryAddSingleton(TimeProvider.System);

// Session authentication reads the tet_session cookie (F1.R6). The fallback policy protects every endpoint,
// so a new endpoint needs [AllowAnonymous] to be public (F1.R1). Challenge returns 401, never a redirect.
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder(SessionAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddControllers();
// ICurrentUser reads the authenticated principal for the request (F1.R9). Ownership checks use its UserId in DAO WHERE clauses.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

var app = builder.Build();

// Migrations run on startup only when Database:MigrateOnStartup is true (Development and tests).
// It is false otherwise, so nothing migrates a production database by accident.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    // Fail fast: a missing connection string must not silently skip the migrations.
    var connectionString = app.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Database:MigrateOnStartup is true but ConnectionStrings:Default is not set.");
    }

    MigrationRunner.Migrate(connectionString, Log.Logger);
}

// Request log: method, path and status only (no query string, headers or bodies). It is placed before the
// error middleware, so a 500 from an unhandled exception is recorded as 500.
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Health is the only endpoint besides the four auth endpoints that is public.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can start the app in integration tests.
public partial class Program { }
