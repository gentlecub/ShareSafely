using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Services;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly IFileMetadataService _metadataService;
    private readonly IConfiguration _config;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(
        IConfiguration config,
        IFileMetadataService metadataService,
        ILogger<AzureBlobStorageService> logger)
    {
        _config = config;
        _metadataService = metadataService;
        _logger = logger;

        var connectionString = config["AzureStorage:ConnectionString"];
        var containerName = config["AzureStorage:ContainerName"];

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<FileResponse> UploadAsync(FileUploadRequest request)
    {
        // Validar archivo
        ValidateFile(request.Archivo);

        // Generar nombre único
        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(request.Archivo.FileName);
        var blobName = $"{fileId}{extension}";

        // Subir a Azure Blob
        var blobClient = _containerClient.GetBlobClient(blobName);

        await using var stream = request.Archivo.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobHttpHeaders
        {
            ContentType = request.Archivo.ContentType
        });

        _logger.LogInformation("Archivo subido a blob: {BlobName}", blobName);

        // Retornar respuesta
        return new FileResponse
        {
            Id = fileId,
            Nombre = request.Archivo.FileName,
            ContentType = request.Archivo.ContentType,
            Tamanio = request.Archivo.Length,
            FechaSubida = DateTime.UtcNow,
            FechaExpiracion = request.ExpiracionMinutos.HasValue
                ? DateTime.UtcNow.AddMinutes(request.ExpiracionMinutos.Value)
                : null,
            Estado = "Activo"
        };
    }

    public async Task<Stream?> DownloadAsync(Guid archivoId)
    {
        var file = await _metadataService.GetByIdAsync(archivoId);
        if (file == null) return null;

        var blobName = GetBlobName(archivoId, file.ContentType);
        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
            return null;

        var download = await blobClient.DownloadStreamingAsync();
        return download.Value.Content;
    }

    public async Task<bool> DeleteAsync(Guid archivoId)
    {
        var file = await _metadataService.GetByIdAsync(archivoId);
        if (file == null) return false;

        var blobName = GetBlobName(archivoId, file.ContentType);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var deleted = await blobClient.DeleteIfExistsAsync();

        if (deleted)
        {
            await _metadataService.UpdateStatusAsync(archivoId, EstadoArchivo.Eliminado);
            _logger.LogInformation("Archivo eliminado: {Id}", archivoId);
        }

        return deleted;
    }

    public async Task<bool> ExistsAsync(Guid archivoId)
    {
        var file = await _metadataService.GetByIdAsync(archivoId);
        if (file == null) return false;

        var blobName = GetBlobName(archivoId, file.ContentType);
        var blobClient = _containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync();
    }

    private void ValidateFile(IFormFile file)
    {
        var maxSize = _config.GetValue<int>("FileValidation:MaxFileSizeMB") * 1024 * 1024;
        var allowedExt = _config.GetSection("FileValidation:AllowedExtensions")
            .Get<string[]>() ?? Array.Empty<string>();

        if (file.Length > maxSize)
            throw new InvalidOperationException($"Archivo excede el límite de {maxSize / 1024 / 1024}MB");

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExt.Contains(ext))
            throw new InvalidOperationException($"Extensión {ext} no permitida");
    }

    private string GetBlobName(Guid fileId, string contentType)
    {
        var ext = MimeTypeToExtension(contentType);
        return $"{fileId}{ext}";
    }

    private string MimeTypeToExtension(string mimeType) => mimeType switch
    {
        "application/pdf" => ".pdf",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        _ => ""
    };
}
