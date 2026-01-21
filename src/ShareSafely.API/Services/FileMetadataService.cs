using Microsoft.EntityFrameworkCore;
using ShareSafely.API.Data;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Services;

public class FileMetadataService : IFileMetadataService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FileMetadataService> _logger;

    public FileMetadataService(
        AppDbContext context,
        ILogger<FileMetadataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FileResponse?> GetByIdAsync(Guid id)
    {
        var archivo = await _context.Archivos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (archivo == null)
            return null;

        return new FileResponse
        {
            Id = archivo.Id,
            Nombre = archivo.NombreOriginal,
            ContentType = archivo.ContentType,
            Tamanio = archivo.Tamanio,
            FechaSubida = archivo.FechaSubida,
            FechaExpiracion = archivo.FechaExpiracion,
            Estado = archivo.Estado.ToString()
        };
    }

    public async Task<List<Archivo>> GetExpiredAsync()
    {
        return await _context.Archivos
            .Where(a => a.Estado == EstadoArchivo.Activo
                     && a.FechaExpiracion.HasValue
                     && a.FechaExpiracion < DateTime.UtcNow)
            .ToListAsync();
    }

    public async Task<bool> UpdateStatusAsync(Guid id, EstadoArchivo estado)
    {
        var archivo = await _context.Archivos.FindAsync(id);
        if (archivo == null)
            return false;

        archivo.Estado = estado;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Estado actualizado: {Id} -> {Estado}", id, estado);
        return true;
    }

    public async Task LogAccessAsync(Guid archivoId, TipoAccion accion, string? ip = null)
    {
        var log = new LogAcceso
        {
            Id = Guid.NewGuid(),
            ArchivoId = archivoId,
            Accion = accion,
            Timestamp = DateTime.UtcNow,
            IpAddress = ip
        };

        _context.LogsAcceso.Add(log);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Log registrado: {Accion} para archivo {Id}", accion, archivoId);
    }

    public async Task<Archivo> CreateAsync(Archivo archivo)
    {
        _context.Archivos.Add(archivo);
        await _context.SaveChangesAsync();
        return archivo;
    }
}
