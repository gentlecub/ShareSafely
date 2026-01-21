using ShareSafely.API.Models.DTOs;

namespace ShareSafely.API.Services.Interfaces;

/// <summary>
/// Servicio para gestión de archivos en Azure Blob Storage
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Sube un archivo a Azure Blob Storage
    /// </summary>
    Task<FileResponse> UploadAsync(FileUploadRequest request);

    /// <summary>
    /// Descarga un archivo por su ID
    /// </summary>
    Task<Stream?> DownloadAsync(Guid archivoId);

    /// <summary>
    /// Elimina un archivo del storage
    /// </summary>
    Task<bool> DeleteAsync(Guid archivoId);

    /// <summary>
    /// Verifica si un archivo existe
    /// </summary>
    Task<bool> ExistsAsync(Guid archivoId);
}
