using System.ComponentModel.DataAnnotations;

namespace ShareSafely.API.Models.DTOs;

/// <summary>
/// Request para generar un nuevo enlace SAS
/// </summary>
public class LinkGenerateRequest
{
    [Required(ErrorMessage = "El ID del archivo es requerido")]
    public Guid ArchivoId { get; set; }

    /// <summary>
    /// Minutos hasta que expire el enlace
    /// </summary>
    [Range(1, 1440, ErrorMessage = "La expiración debe ser entre 1 y 1440 minutos")]
    public int ExpiracionMinutos { get; set; } = 60;
}
