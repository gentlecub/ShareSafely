using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ShareSafely.API.Exceptions;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Services;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly IFileMetadataService _metadataService;
    private readonly IConfiguration _config;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private readonly Lazy<BlobContainerClient> _lazyContainerClient;

    public AzureBlobStorageService(
        IConfiguration config,
        IFileMetadataService metadataService,
        ILogger<AzureBlobStorageService> logger)
    {
        _config = config;
        _metadataService = metadataService;
        _logger = logger;

        // Lazy initialization - no network call in constructor
        _lazyContainerClient = new Lazy<BlobContainerClient>(() => InitializeContainer());
    }

    private BlobContainerClient ContainerClient => _lazyContainerClient.Value;

    private BlobContainerClient InitializeContainer()
    {
        var connectionString = _config["AzureStorage:ConnectionString"];
        var containerName = _config["AzureStorage:ContainerName"];

        if (string.IsNullOrEmpty(connectionString))
            throw new StorageException("Azure Storage connection string not configured");

        if (string.IsNullOrEmpty(containerName))
            throw new StorageException("Azure Storage container name not configured");

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
        var container = blobServiceClient.GetBlobContainerClient(containerName);

        try
        {
            container.CreateIfNotExists(PublicAccessType.None);
            _logger.LogInformation("Blob container initialized: {Container}", containerName);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to initialize blob container: {Container}", containerName);
            throw new StorageException($"Failed to initialize storage container: {containerName}", ex);
        }

        return container;
    }

    public async Task<FileResponse> UploadAsync(FileUploadRequest request)
    {
        // Validate file
        ValidateFile(request.Archivo);

        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(request.Archivo.FileName);
        var blobName = $"{fileId}{extension}";

        try
        {
            var blobClient = ContainerClient.GetBlobClient(blobName);

            await using var stream = request.Archivo.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = request.Archivo.ContentType
            });

            _logger.LogInformation("File uploaded to blob: {BlobName}, Size: {Size}", blobName, request.Archivo.Length);

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
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure storage error during upload: {BlobName}", blobName);
            throw new StorageException($"Failed to upload file: {blobName}", ex);
        }
    }

    public async Task<Stream?> DownloadAsync(Guid archivoId)
    {
        var file = await _metadataService.GetByIdAsync(archivoId);
        if (file == null)
        {
            _logger.LogWarning("Download requested for non-existent file: {Id}", archivoId);
            return null;
        }

        var blobName = GetBlobName(archivoId, file.ContentType);

        try
        {
            var blobClient = ContainerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Blob not found for file: {Id}, Blob: {BlobName}", archivoId, blobName);
                return null;
            }

            var download = await blobClient.DownloadStreamingAsync();
            return download.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Blob not found: {BlobName}", blobName);
            return null;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure storage error during download: {BlobName}", blobName);
            throw new StorageException($"Failed to download file: {blobName}", ex);
        }
    }

    public async Task<bool> DeleteAsync(Guid archivoId)
    {
        var file = await _metadataService.GetByIdAsync(archivoId);
        if (file == null)
        {
            _logger.LogWarning("Delete requested for non-existent file: {Id}", archivoId);
            return false;
        }

        var blobName = GetBlobName(archivoId, file.ContentType);

        try
        {
            var blobClient = ContainerClient.GetBlobClient(blobName);
            var deleted = await blobClient.DeleteIfExistsAsync();

            if (deleted)
            {
                await _metadataService.UpdateStatusAsync(archivoId, EstadoArchivo.Eliminado);
                _logger.LogInformation("File deleted: {Id}, Blob: {BlobName}", archivoId, blobName);
            }
            else
            {
                _logger.LogWarning("Blob not found during delete: {BlobName}", blobName);
            }

            return deleted;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure storage error during delete: {BlobName}", blobName);
            throw new StorageException($"Failed to delete file: {blobName}", ex);
        }
    }

    public async Task<bool> ExistsAsync(Guid archivoId)
    {
        var file = await _metadataService.GetByIdAsync(archivoId);
        if (file == null) return false;

        var blobName = GetBlobName(archivoId, file.ContentType);

        try
        {
            var blobClient = ContainerClient.GetBlobClient(blobName);
            return await blobClient.ExistsAsync();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure storage error checking existence: {BlobName}", blobName);
            throw new StorageException($"Failed to check file existence: {blobName}", ex);
        }
    }

    private void ValidateFile(IFormFile file)
    {
        var maxSizeMB = _config.GetValue<int>("FileValidation:MaxFileSizeMB", 100);
        var maxSize = maxSizeMB * 1024L * 1024L;
        var allowedExt = _config.GetSection("FileValidation:AllowedExtensions")
            .Get<string[]>() ?? Array.Empty<string>();

        if (file.Length == 0)
            throw new ValidationException("El archivo está vacío");

        if (file.Length > maxSize)
            throw new ValidationException($"El archivo excede el límite de {maxSizeMB}MB");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
            throw new ValidationException("El archivo debe tener una extensión");

        if (!allowedExt.Contains(ext))
            throw new ValidationException($"Extensión {ext} no permitida. Permitidas: {string.Join(", ", allowedExt)}");
    }

    private static string GetBlobName(Guid fileId, string contentType)
    {
        var ext = MimeTypeToExtension(contentType);
        return $"{fileId}{ext}";
    }

    private static string MimeTypeToExtension(string mimeType) => mimeType switch
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
