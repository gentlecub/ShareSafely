using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Services;

public class SasLinkService : ISasLinkService
{
    private readonly BlobContainerClient _containerClient;
    private readonly IFileMetadataService _metadataService;
    private readonly IConfiguration _config;
    private readonly ILogger<SasLinkService> _logger;

    public SasLinkService(
        IConfiguration config,
        IFileMetadataService metadataService,
        ILogger<SasLinkService> logger)
    {
        _config = config;
        _metadataService = metadataService;
        _logger = logger;

        var connectionString = config["AzureStorage:ConnectionString"];
        var containerName = config["AzureStorage:ContainerName"];

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<LinkResponse> GenerateAsync(LinkGenerateRequest request)
    {
        var file = await _metadataService.GetByIdAsync(request.ArchivoId);
        if (file == null)
            throw new InvalidOperationException("Archivo no encontrado");

        // Obtener blob
        var blobName = $"{request.ArchivoId}{GetExtension(file.ContentType)}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
            throw new InvalidOperationException("Archivo no existe en storage");

        // Generar SAS Token
        var expiration = DateTimeOffset.UtcNow.AddMinutes(request.ExpiracionMinutos);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = expiration
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        var token = Guid.NewGuid().ToString("N");

        _logger.LogInformation("SAS generado para archivo: {Id}", request.ArchivoId);

        return new LinkResponse
        {
            Id = Guid.NewGuid(),
            ArchivoId = request.ArchivoId,
            Url = sasUri.ToString(),
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = expiration.DateTime,
            Estado = "Activo"
        };
    }

    public async Task<bool> ValidateAsync(string token)
    {
        // TODO: Validar token contra BD
        // Por ahora retorna true para testing
        await Task.CompletedTask;
        return !string.IsNullOrEmpty(token);
    }

    public async Task<string?> GetDownloadUrlAsync(string token)
    {
        // TODO: Obtener URL desde BD por token
        await Task.CompletedTask;
        return null;
    }

    public async Task<bool> RevokeAsync(Guid enlaceId)
    {
        // TODO: Marcar enlace como revocado en BD
        await Task.CompletedTask;
        _logger.LogInformation("Enlace revocado: {Id}", enlaceId);
        return true;
    }

    private string GetExtension(string contentType) => contentType switch
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
