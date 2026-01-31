using ShareSafely.API.Exceptions;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Services;

/// <summary>
/// Servicio de almacenamiento local para demos cuando Azure Storage no está disponible.
/// NOTA: Los archivos se pierden al reiniciar el contenedor.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly IFileMetadataService _metadataService;
    private readonly IConfiguration _config;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _storagePath;

    public LocalFileStorageService(
        IConfiguration config,
        IFileMetadataService metadataService,
        ILogger<LocalFileStorageService> logger)
    {
        _config = config;
        _metadataService = metadataService;
        _logger = logger;

        _storagePath = config["LocalStorage:Path"] ?? "/tmp/sharesafely-files";

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
            _logger.LogInformation("Local storage initialized at: {Path}", _storagePath);
        }
    }

    public async Task<FileResponse> UploadAsync(FileUploadRequest request)
    {
        ValidateFile(request.Archivo);

        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(request.Archivo.FileName);
        var fileName = $"{fileId}{extension}";
        var filePath = Path.Combine(_storagePath, fileName);

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Create);
            await request.Archivo.CopyToAsync(stream);

            _logger.LogInformation("File uploaded locally: {FileName}, Size: {Size}", fileName, request.Archivo.Length);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file locally: {FileName}", fileName);
            throw new StorageException($"Failed to upload file: {fileName}", ex);
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

        var fileName = GetFileName(archivoId, file.ContentType);
        var filePath = Path.Combine(_storagePath, fileName);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {Path}", filePath);
            return null;
        }

        return new FileStream(filePath, FileMode.Open, FileAccess.Read);
    }

    public async Task<bool> DeleteAsync(Guid archivoId)
    {
        var file = await _metadataService.GetByIdAsync(archivoId);
        if (file == null)
        {
            _logger.LogWarning("Delete requested for non-existent file: {Id}", archivoId);
            return false;
        }

        var fileName = GetFileName(archivoId, file.ContentType);
        var filePath = Path.Combine(_storagePath, fileName);

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                await _metadataService.UpdateStatusAsync(archivoId, EstadoArchivo.Eliminado);
                _logger.LogInformation("File deleted: {Path}", filePath);
                return true;
            }

            _logger.LogWarning("File not found during delete: {Path}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {Path}", filePath);
            throw new StorageException($"Failed to delete file: {fileName}", ex);
        }
    }

    public async Task<bool> ExistsAsync(Guid archivoId)
    {
        var file = await _metadataService.GetByIdAsync(archivoId);
        if (file == null) return false;

        var fileName = GetFileName(archivoId, file.ContentType);
        var filePath = Path.Combine(_storagePath, fileName);

        return File.Exists(filePath);
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

    private static string GetFileName(Guid fileId, string contentType)
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
