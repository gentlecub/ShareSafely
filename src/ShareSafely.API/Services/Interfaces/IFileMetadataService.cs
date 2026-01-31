using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;

namespace ShareSafely.API.Services.Interfaces;

/// <summary>
/// Servicio para gestión de metadata en base de datos
/// </summary>
public interface IFileMetadataService
{
    /// <summary>
    /// Obtiene info de un archivo por ID
    /// </summary>
    Task<FileResponse?> GetByIdAsync(Guid id);

    /// <summary>
    /// Obtiene archivos expirados para limpieza
    /// </summary>
    Task<List<Archivo>> GetExpiredAsync();

    /// <summary>
    /// Actualiza el estado de un archivo
    /// </summary>
    Task<bool> UpdateStatusAsync(Guid id, EstadoArchivo estado);

    /// <summary>
    /// Registra un acceso en el log
    /// </summary>
    Task LogAccessAsync(Guid archivoId, TipoAccion accion, string? ip = null);

    /// <summary>
    /// Crea un nuevo registro de archivo
    /// </summary>
    Task<Archivo> CreateAsync(Archivo archivo);

    /// <summary>
    /// Elimina un registro de archivo
    /// </summary>
    Task<bool> DeleteAsync(Guid id);
}
