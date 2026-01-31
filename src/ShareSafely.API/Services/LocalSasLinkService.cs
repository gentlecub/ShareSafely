using Microsoft.EntityFrameworkCore;
using ShareSafely.API.Data;
using ShareSafely.API.Exceptions;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Services;

/// <summary>
/// Servicio de enlaces para almacenamiento local (demos).
/// Genera URLs internas de descarga en lugar de SAS tokens de Azure.
/// </summary>
public class LocalSasLinkService : ISasLinkService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<LocalSasLinkService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalSasLinkService(
        AppDbContext context,
        IConfiguration config,
        ILogger<LocalSasLinkService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _config = config;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<LinkResponse> GenerateAsync(LinkGenerateRequest request)
    {
        var maxExpiration = _config.GetValue<int>("SasLink:MaxExpirationMinutes", 1440);
        if (request.ExpiracionMinutos > maxExpiration)
        {
            throw new ValidationException($"La expiración máxima permitida es {maxExpiration} minutos");
        }

        var archivo = await _context.Archivos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ArchivoId && a.Estado == EstadoArchivo.Activo);

        if (archivo == null)
        {
            _logger.LogWarning("Link generation attempted for non-existent file: {Id}", request.ArchivoId);
            throw new NotFoundException("Archivo", request.ArchivoId);
        }

        if (archivo.FechaExpiracion.HasValue && archivo.FechaExpiracion < DateTime.UtcNow)
        {
            _logger.LogWarning("Link generation attempted for expired file: {Id}", request.ArchivoId);
            throw new ValidationException("El archivo ha expirado");
        }

        var token = Guid.NewGuid().ToString("N");
        var expiration = DateTime.UtcNow.AddMinutes(request.ExpiracionMinutos);

        // Generate internal download URL
        var baseUrl = GetBaseUrl();
        var downloadUrl = $"{baseUrl}/api/links/download/{token}";

        var enlace = new Enlace
        {
            Id = Guid.NewGuid(),
            ArchivoId = request.ArchivoId,
            Token = token,
            UrlCompleta = downloadUrl,
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = expiration,
            Estado = EstadoEnlace.Activo,
            AccesosCount = 0
        };

        _context.Enlaces.Add(enlace);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Local link generated for file: {FileId}, Link: {LinkId}, Expires: {Expiration}",
            request.ArchivoId, enlace.Id, expiration);

        return new LinkResponse
        {
            Id = enlace.Id,
            ArchivoId = request.ArchivoId,
            Url = downloadUrl,
            Token = token,
            FechaCreacion = enlace.FechaCreacion,
            FechaExpiracion = expiration,
            Estado = "Activo"
        };
    }

    public async Task<bool> ValidateAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var enlace = await _context.Enlaces
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Token == token);

        if (enlace == null)
        {
            _logger.LogWarning("Validation attempted with unknown token: {Token}", token);
            return false;
        }

        if (enlace.Estado != EstadoEnlace.Activo)
        {
            _logger.LogWarning("Validation attempted with inactive token: {Token}, Status: {Status}",
                token, enlace.Estado);
            return false;
        }

        if (enlace.FechaExpiracion < DateTime.UtcNow)
        {
            _logger.LogWarning("Validation attempted with expired token: {Token}", token);

            var linkToUpdate = await _context.Enlaces.FindAsync(enlace.Id);
            if (linkToUpdate != null)
            {
                linkToUpdate.Estado = EstadoEnlace.Expirado;
                await _context.SaveChangesAsync();
            }

            return false;
        }

        return true;
    }

    public async Task<string?> GetDownloadUrlAsync(string token)
    {
        var enlace = await _context.Enlaces
            .Include(e => e.Archivo)
            .FirstOrDefaultAsync(e => e.Token == token && e.Estado == EstadoEnlace.Activo);

        if (enlace == null)
        {
            _logger.LogWarning("Download URL requested for invalid token: {Token}", token);
            return null;
        }

        if (enlace.FechaExpiracion < DateTime.UtcNow)
        {
            _logger.LogWarning("Download URL requested for expired token: {Token}", token);
            enlace.Estado = EstadoEnlace.Expirado;
            await _context.SaveChangesAsync();
            return null;
        }

        enlace.AccesosCount++;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Download initiated via token: {Token}, Access count: {Count}",
            token, enlace.AccesosCount);

        // Return internal marker for local download
        return $"LOCAL:{enlace.ArchivoId}";
    }

    public async Task<bool> RevokeAsync(Guid enlaceId)
    {
        var enlace = await _context.Enlaces.FindAsync(enlaceId);

        if (enlace == null)
        {
            _logger.LogWarning("Revoke attempted for non-existent link: {Id}", enlaceId);
            return false;
        }

        if (enlace.Estado == EstadoEnlace.Revocado)
        {
            _logger.LogWarning("Revoke attempted for already revoked link: {Id}", enlaceId);
            return false;
        }

        enlace.Estado = EstadoEnlace.Revocado;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Link revoked: {Id}", enlaceId);
        return true;
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }

        // Fallback
        return _config["Application:BaseUrl"] ?? "http://localhost:5000";
    }
}
