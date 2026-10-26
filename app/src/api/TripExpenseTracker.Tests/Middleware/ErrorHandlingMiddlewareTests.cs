using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TripExpenseTracker.Api.Middleware;

namespace TripExpenseTracker.Tests.Middleware;

public class ErrorHandlingMiddlewareTests
{
    private const string SecretDetail = "secret internal detail";

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetails_WithoutExceptionText()
    {
        var middleware = new ErrorHandlingMiddleware(
            _ => throw new InvalidOperationException(SecretDetail),
            NullLogger<ErrorHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        body.Position = 0;
        var json = await new StreamReader(body).ReadToEndAsync();

        Assert.DoesNotContain(SecretDetail, json);
        Assert.DoesNotContain("InvalidOperationException", json);
        Assert.DoesNotContain("StackTrace", json);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", document.RootElement.GetProperty("title").GetString());
    }
}
