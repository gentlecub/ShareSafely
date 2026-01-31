namespace ShareSafely.API.Exceptions;

/// <summary>
/// Base exception for all ShareSafely business exceptions
/// </summary>
public abstract class ShareSafelyException : Exception
{
    public int StatusCode { get; }
    public string UserMessage { get; }

    protected ShareSafelyException(string message, string userMessage, int statusCode, Exception? inner = null)
        : base(message, inner)
    {
        UserMessage = userMessage;
        StatusCode = statusCode;
    }
}

/// <summary>
/// Validation errors (400 Bad Request)
/// </summary>
public class ValidationException : ShareSafelyException
{
    public ValidationException(string message)
        : base(message, message, 400) { }

    public ValidationException(string message, string userMessage)
        : base(message, userMessage, 400) { }
}

/// <summary>
/// Resource not found (404 Not Found)
/// </summary>
public class NotFoundException : ShareSafelyException
{
    public NotFoundException(string resource)
        : base($"{resource} not found", $"{resource} no encontrado", 404) { }

    public NotFoundException(string resource, Guid id)
        : base($"{resource} with ID {id} not found", $"{resource} no encontrado", 404) { }
}

/// <summary>
/// Azure storage errors (503 Service Unavailable)
/// </summary>
public class StorageException : ShareSafelyException
{
    public StorageException(string message, Exception? inner = null)
        : base(message, "Error de almacenamiento. Intente nuevamente.", 503, inner) { }
}

/// <summary>
/// Database errors (503 Service Unavailable)
/// </summary>
public class DatabaseException : ShareSafelyException
{
    public DatabaseException(string message, Exception? inner = null)
        : base(message, "Error de base de datos. Intente nuevamente.", 503, inner) { }
}

/// <summary>
/// Authentication errors (401 Unauthorized)
/// </summary>
public class AuthenticationException : ShareSafelyException
{
    public AuthenticationException(string message = "Authentication required")
        : base(message, "Autenticación requerida", 401) { }
}

/// <summary>
/// Authorization errors (403 Forbidden)
/// </summary>
public class AuthorizationException : ShareSafelyException
{
    public AuthorizationException(string message = "Access denied")
        : base(message, "Acceso denegado", 403) { }
}

/// <summary>
/// Link expired or invalid (410 Gone)
/// </summary>
public class LinkExpiredException : ShareSafelyException
{
    public LinkExpiredException(string token)
        : base($"Link with token {token} has expired or been revoked", "Enlace expirado o revocado", 410) { }
}
