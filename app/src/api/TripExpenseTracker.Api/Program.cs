using Serilog;
using Serilog.Events;
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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so WebApplicationFactory<Program> can start the app in integration tests.
public partial class Program { }
