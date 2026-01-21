namespace ShareSafely.API.Models.Entities;

/// <summary>
/// Representa un enlace SAS generado para compartir un archivo
/// </summary>
public class Enlace
{
    public Guid Id { get; set; }
    public Guid ArchivoId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string UrlCompleta { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaExpiracion { get; set; }
    public EstadoEnlace Estado { get; set; }
    public int AccesosCount { get; set; }

    // Navegación
    public Archivo? Archivo { get; set; }
}

public enum EstadoEnlace
{
    Activo = 1,
    Expirado = 2,
    Revocado = 3
}
