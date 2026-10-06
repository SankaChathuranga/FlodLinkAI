using System.Net;
using System.Text.Json;
using FloodLink.Domain.Exceptions;

namespace FloodLink.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception encountered: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        int statusCode;
        string message;

        switch (exception)
        {
            case NotFoundException notFoundEx:
                statusCode = (int)HttpStatusCode.NotFound; // 404
                message = notFoundEx.Message;
                break;
            case BadRequestException badRequestEx:
                statusCode = (int)HttpStatusCode.BadRequest; // 400
                message = badRequestEx.Message;
                break;
            case UnauthorizedException unauthEx:
                statusCode = (int)HttpStatusCode.Unauthorized; // 401
                message = unauthEx.Message;
                break;
            case ForbiddenException forbiddenEx:
                statusCode = (int)HttpStatusCode.Forbidden; // 403
                message = forbiddenEx.Message;
                break;
            default:
                statusCode = (int)HttpStatusCode.InternalServerError; // 500
                message = "An unexpected error occurred on the server.";
                break;
        }

        context.Response.StatusCode = statusCode;

        var payload = new
        {
            statusCode,
            message,
            detail = statusCode == 500 ? exception.Message : null
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, jsonOptions));
    }
}
