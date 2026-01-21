namespace ShareSafely.API.Models.DTOs;

/// <summary>
/// Response después de subir un archivo
/// </summary>
public class FileResponse
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Tamanio { get; set; }
    public DateTime FechaSubida { get; set; }
    public DateTime? FechaExpiracion { get; set; }
    public string Estado { get; set; } = string.Empty;
}
