namespace ShareSafely.API.Models.DTOs;

/// <summary>
/// Response después de generar un enlace SAS
/// </summary>
public class LinkResponse
{
    public Guid Id { get; set; }
    public Guid ArchivoId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaExpiracion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int AccesosCount { get; set; }
}
