using System.ComponentModel.DataAnnotations;

namespace ShareSafely.API.Models.DTOs;

/// <summary>
/// Request para subir un archivo
/// </summary>
public class FileUploadRequest
{
    [Required(ErrorMessage = "El archivo es requerido")]
    public IFormFile Archivo { get; set; } = null!;

    /// <summary>
    /// Minutos hasta que expire el archivo (opcional)
    /// </summary>
    [Range(1, 1440, ErrorMessage = "La expiración debe ser entre 1 y 1440 minutos")]
    public int? ExpiracionMinutos { get; set; }
}
