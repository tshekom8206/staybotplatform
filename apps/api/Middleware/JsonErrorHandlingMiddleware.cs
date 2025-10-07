using System.Net;
using System.Text.Json;

namespace Hostr.Api.Middleware;

public class JsonErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JsonErrorHandlingMiddleware> _logger;

    public JsonErrorHandlingMiddleware(RequestDelegate next, ILogger<JsonErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            // Handle authentication/authorization failures
            if (context.Response.StatusCode == (int)HttpStatusCode.Unauthorized ||
                context.Response.StatusCode == (int)HttpStatusCode.Forbidden)
            {
                await HandleUnauthorizedAsync(context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleUnauthorizedAsync(HttpContext context)
    {
        if (!context.Response.HasStarted && !context.Response.Headers.ContainsKey("Content-Type"))
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "Unauthorized",
                message = context.Response.StatusCode == (int)HttpStatusCode.Unauthorized
                    ? "Authentication required"
                    : "Access forbidden",
                statusCode = context.Response.StatusCode
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                error = "Internal Server Error",
                message = "An error occurred while processing your request",
                statusCode = context.Response.StatusCode
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}