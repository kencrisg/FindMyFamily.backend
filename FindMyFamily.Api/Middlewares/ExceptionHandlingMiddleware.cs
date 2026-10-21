using System.Net;
using System.Text.Json;
using FindMyFamily.Api.Common;
using FindMyFamily.Core.Exceptions;

namespace FindMyFamily.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        HttpStatusCode statusCode;
        string errorCode;
        string message;

        if (exception is AppException appEx)
        {
            statusCode = appEx.StatusCode;
            errorCode = appEx.Code;
            message = appEx.Message;
            _logger.LogWarning(exception, "Domain exception: {ErrorCode} - {Message}", errorCode, message);
        }
        else
        {
            statusCode = HttpStatusCode.InternalServerError;
            errorCode = "INTERNAL_SERVER_ERROR";
            message = "Ha ocurrido un error inesperado en el servidor.";
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }

        response.StatusCode = (int)statusCode;

        var errorResponse = new ErrorResponse(
            Error: true,
            Code: errorCode,
            Message: message
        );

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(json);
    }
}
