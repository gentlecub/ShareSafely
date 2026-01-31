using System.Diagnostics;
using System.Text.Json;
using ShareSafely.API.Exceptions;
using ShareSafely.API.Models.DTOs;

namespace ShareSafely.API.Middleware;

/// <summary>
/// Global exception handler middleware that catches all unhandled exceptions
/// and returns consistent JSON error responses
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
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
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var (statusCode, userMessage) = exception switch
        {
            // Business exceptions with known status codes
            ShareSafelyException ex => (ex.StatusCode, ex.UserMessage),

            // Azure storage errors
            Azure.RequestFailedException ex => HandleAzureException(ex),

            // Database errors
            Microsoft.Data.SqlClient.SqlException ex => HandleSqlException(ex),

            // EF Core database errors
            Microsoft.EntityFrameworkCore.DbUpdateException ex => HandleDbUpdateException(ex),

            // Request cancelled by client
            OperationCanceledException => (499, "Solicitud cancelada"),

            // Invalid operation (legacy - treat as validation)
            InvalidOperationException ex => (400, GetSafeValidationMessage(ex.Message)),

            // Argument exceptions
            ArgumentException ex => (400, GetSafeValidationMessage(ex.Message)),

            // Everything else is an internal error
            _ => (500, _env.IsDevelopment() ? exception.Message : "Error interno del servidor")
        };

        // Log based on severity
        LogException(exception, statusCode, traceId);

        // Don't override response if already started
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started, cannot write error response");
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            Success = false,
            Message = userMessage,
            TraceId = traceId,
            Errors = _env.IsDevelopment() ? new List<string> { exception.Message } : new List<string>()
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private (int statusCode, string message) HandleAzureException(Azure.RequestFailedException ex)
    {
        return ex.Status switch
        {
            404 => (404, "Archivo no encontrado en almacenamiento"),
            403 => (503, "Error de permisos en almacenamiento"),
            409 => (409, "Conflicto en operación de almacenamiento"),
            _ => (503, "Servicio de almacenamiento temporalmente no disponible")
        };
    }

    private (int statusCode, string message) HandleSqlException(Microsoft.Data.SqlClient.SqlException ex)
    {
        // Log full details but return safe message
        return ex.Number switch
        {
            // Connection errors
            -2 or 53 or 10060 => (503, "Base de datos temporalmente no disponible"),
            // Deadlock
            1205 => (503, "Operación bloqueada, intente nuevamente"),
            // Constraint violation
            547 => (400, "Operación viola restricciones de datos"),
            // Unique constraint
            2601 or 2627 => (409, "El recurso ya existe"),
            // Default
            _ => (503, "Error de base de datos")
        };
    }

    private (int statusCode, string message) HandleDbUpdateException(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            return HandleSqlException(sqlEx);
        }
        return (503, "Error al guardar datos");
    }

    private static string GetSafeValidationMessage(string message)
    {
        // Map known validation messages, sanitize unknown ones
        if (message.Contains("límite", StringComparison.OrdinalIgnoreCase))
            return "El archivo excede el tamaño máximo permitido";
        if (message.Contains("permitida", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("extensión", StringComparison.OrdinalIgnoreCase))
            return "Tipo de archivo no permitido";
        if (message.Contains("no encontrado", StringComparison.OrdinalIgnoreCase))
            return "Recurso no encontrado";
        if (message.Contains("no existe", StringComparison.OrdinalIgnoreCase))
            return "Recurso no existe";
        if (message.Contains("requerido", StringComparison.OrdinalIgnoreCase))
            return message; // Validation messages are safe
        if (message.Contains("inválido", StringComparison.OrdinalIgnoreCase))
            return "Datos inválidos";

        // Default safe message for unknown errors
        return "Error de validación";
    }

    private void LogException(Exception exception, int statusCode, string traceId)
    {
        var logLevel = statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 and < 500 => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(
            logLevel,
            exception,
            "Exception handled: {ExceptionType} - Status: {StatusCode} - TraceId: {TraceId}",
            exception.GetType().Name,
            statusCode,
            traceId);
    }
}

/// <summary>
/// Error response DTO for consistent error format
/// </summary>
public class ErrorResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Extension method to register the middleware
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
