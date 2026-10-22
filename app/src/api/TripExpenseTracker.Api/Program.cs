using Serilog;
using Serilog.Events;
using TripExpenseTracker.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Serilog is the only logging output. Console is a Serilog sink, not a Console.Write call.
builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so WebApplicationFactory<Program> can start the app in integration tests.
public partial class Program { }
