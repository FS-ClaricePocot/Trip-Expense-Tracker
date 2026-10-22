using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using TripExpenseTracker.Api.Auth;
using TripExpenseTracker.Api.Middleware;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Data;

var builder = WebApplication.CreateBuilder(args);

// Serilog is the only logging output. Console is a Serilog sink, not a Console.Write call.
builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

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

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Health is the only endpoint besides the four auth endpoints that is public.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can start the app in integration tests.
public partial class Program { }
