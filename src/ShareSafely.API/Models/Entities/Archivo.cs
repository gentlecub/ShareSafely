namespace ShareSafely.API.Models.Entities;

/// <summary>
/// Representa un archivo subido al sistema
/// </summary>
public class Archivo
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string NombreOriginal { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Tamanio { get; set; }
    public DateTime FechaSubida { get; set; }
    public DateTime? FechaExpiracion { get; set; }
    public EstadoArchivo Estado { get; set; }
    public string BlobUrl { get; set; } = string.Empty;

    // Navegación
    public ICollection<Enlace> Enlaces { get; set; } = new List<Enlace>();
    public ICollection<LogAcceso> Logs { get; set; } = new List<LogAcceso>();
}

public enum EstadoArchivo
{
    Activo = 1,
    Expirado = 2,
    Eliminado = 3
}
