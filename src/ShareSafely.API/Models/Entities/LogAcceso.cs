namespace ShareSafely.API.Models.Entities;

/// <summary>
/// Registro de acciones realizadas sobre archivos
/// </summary>
public class LogAcceso
{
    public Guid Id { get; set; }
    public Guid ArchivoId { get; set; }
    public Guid? EnlaceId { get; set; }
    public TipoAccion Accion { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Navegación
    public Archivo? Archivo { get; set; }
}

public enum TipoAccion
{
    Subida = 1,
    Descarga = 2,
    EnlaceGenerado = 3,
    EnlaceExpirado = 4,
    ArchivoEliminado = 5
}
