using ShareSafely.API.Models.DTOs;

namespace ShareSafely.API.Services.Interfaces;

/// <summary>
/// Servicio para generación de enlaces SAS
/// </summary>
public interface ISasLinkService
{
    /// <summary>
    /// Genera un nuevo enlace SAS para un archivo
    /// </summary>
    Task<LinkResponse> GenerateAsync(LinkGenerateRequest request);

    /// <summary>
    /// Valida si un enlace/token es válido y no ha expirado
    /// </summary>
    Task<bool> ValidateAsync(string token);

    /// <summary>
    /// Obtiene la URL de descarga si el token es válido
    /// </summary>
    Task<string?> GetDownloadUrlAsync(string token);

    /// <summary>
    /// Revoca un enlace antes de su expiración
    /// </summary>
    Task<bool> RevokeAsync(Guid enlaceId);
}
