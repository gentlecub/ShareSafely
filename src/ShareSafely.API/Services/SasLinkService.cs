using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.EntityFrameworkCore;
using ShareSafely.API.Data;
using ShareSafely.API.Exceptions;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Services;

public class SasLinkService : ISasLinkService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<SasLinkService> _logger;
    private readonly Lazy<BlobContainerClient> _lazyContainerClient;

    public SasLinkService(
        AppDbContext context,
        IConfiguration config,
        ILogger<SasLinkService> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;

        _lazyContainerClient = new Lazy<BlobContainerClient>(() => InitializeContainer());
    }

    private BlobContainerClient ContainerClient => _lazyContainerClient.Value;

    private BlobContainerClient InitializeContainer()
    {
        var connectionString = _config["AzureStorage:ConnectionString"];
        var containerName = _config["AzureStorage:ContainerName"];

        if (string.IsNullOrEmpty(connectionString))
            throw new StorageException("Azure Storage connection string not configured");

        var options = new BlobClientOptions
        {
            Retry =
            {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(10),
                Mode = Azure.Core.RetryMode.Exponential
            }
        };

        var blobServiceClient = new BlobServiceClient(connectionString, options);
        return blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<LinkResponse> GenerateAsync(LinkGenerateRequest request)
    {
        // Validate expiration limits
        var maxExpiration = _config.GetValue<int>("SasLink:MaxExpirationMinutes", 1440);
        if (request.ExpiracionMinutos > maxExpiration)
        {
            throw new ValidationException($"La expiración máxima permitida es {maxExpiration} minutos");
        }

        // Get file from database
        var archivo = await _context.Archivos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ArchivoId && a.Estado == EstadoArchivo.Activo);

        if (archivo == null)
        {
            _logger.LogWarning("Link generation attempted for non-existent file: {Id}", request.ArchivoId);
            throw new NotFoundException("Archivo", request.ArchivoId);
        }

        // Check if file has expired
        if (archivo.FechaExpiracion.HasValue && archivo.FechaExpiracion < DateTime.UtcNow)
        {
            _logger.LogWarning("Link generation attempted for expired file: {Id}", request.ArchivoId);
            throw new ValidationException("El archivo ha expirado");
        }

        // Build blob name
        var blobName = $"{request.ArchivoId}{GetExtension(archivo.ContentType)}";

        try
        {
            var blobClient = ContainerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogError("Blob not found for file: {Id}, Blob: {BlobName}", request.ArchivoId, blobName);
                throw new StorageException($"Archivo no encontrado en almacenamiento: {blobName}");
            }

            // Generate SAS Token
            var expiration = DateTimeOffset.UtcNow.AddMinutes(request.ExpiracionMinutos);
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = ContainerClient.Name,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = expiration
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            var token = Guid.NewGuid().ToString("N");

            // Save link to database
            var enlace = new Enlace
            {
                Id = Guid.NewGuid(),
                ArchivoId = request.ArchivoId,
                Token = token,
                UrlCompleta = sasUri.ToString(),
                FechaCreacion = DateTime.UtcNow,
                FechaExpiracion = expiration.DateTime,
                Estado = EstadoEnlace.Activo,
                AccesosCount = 0
            };

            _context.Enlaces.Add(enlace);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SAS link generated for file: {FileId}, Link: {LinkId}, Expires: {Expiration}",
                request.ArchivoId, enlace.Id, expiration);

            return new LinkResponse
            {
                Id = enlace.Id,
                ArchivoId = request.ArchivoId,
                Url = sasUri.ToString(),
                Token = token,
                FechaCreacion = enlace.FechaCreacion,
                FechaExpiracion = expiration.DateTime,
                Estado = "Activo"
            };
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure storage error generating SAS for: {BlobName}", blobName);
            throw new StorageException($"Error al generar enlace para: {blobName}", ex);
        }
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

        // Check if link is still active
        if (enlace.Estado != EstadoEnlace.Activo)
        {
            _logger.LogWarning("Validation attempted with inactive token: {Token}, Status: {Status}",
                token, enlace.Estado);
            return false;
        }

        // Check if link has expired
        if (enlace.FechaExpiracion < DateTime.UtcNow)
        {
            _logger.LogWarning("Validation attempted with expired token: {Token}", token);

            // Mark as expired
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

        // Increment access count
        enlace.AccesosCount++;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Download initiated via token: {Token}, Access count: {Count}",
            token, enlace.AccesosCount);

        return enlace.UrlCompleta;
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

    private static string GetExtension(string contentType) => contentType switch
    {
        "application/pdf" => ".pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "application/zip" => ".zip",
        _ => ""
    };
}
