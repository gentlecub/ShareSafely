using Microsoft.EntityFrameworkCore;
using ShareSafely.API.Data;
using ShareSafely.API.Exceptions;
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
        try
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
        catch (Exception ex) when (ex is not ShareSafelyException)
        {
            _logger.LogError(ex, "Database error getting file by ID: {Id}", id);
            throw new DatabaseException($"Error retrieving file {id}", ex);
        }
    }

    public async Task<List<Archivo>> GetExpiredAsync()
    {
        try
        {
            return await _context.Archivos
                .Where(a => a.Estado == EstadoArchivo.Activo
                         && a.FechaExpiracion.HasValue
                         && a.FechaExpiracion < DateTime.UtcNow)
                .ToListAsync();
        }
        catch (Exception ex) when (ex is not ShareSafelyException)
        {
            _logger.LogError(ex, "Database error getting expired files");
            throw new DatabaseException("Error retrieving expired files", ex);
        }
    }

    public async Task<bool> UpdateStatusAsync(Guid id, EstadoArchivo estado)
    {
        try
        {
            var archivo = await _context.Archivos.FindAsync(id);
            if (archivo == null)
            {
                _logger.LogWarning("Attempted to update status of non-existent file: {Id}", id);
                return false;
            }

            var previousStatus = archivo.Estado;
            archivo.Estado = estado;
            await _context.SaveChangesAsync();

            _logger.LogInformation("File status updated: {Id}, {OldStatus} -> {NewStatus}",
                id, previousStatus, estado);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict updating file status: {Id}", id);
            throw new DatabaseException($"Concurrency conflict updating file {id}", ex);
        }
        catch (Exception ex) when (ex is not ShareSafelyException)
        {
            _logger.LogError(ex, "Database error updating file status: {Id}", id);
            throw new DatabaseException($"Error updating file status {id}", ex);
        }
    }

    public async Task LogAccessAsync(Guid archivoId, TipoAccion accion, string? ip = null)
    {
        try
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

            _logger.LogInformation("Access logged: {Action} for file {Id} from IP {Ip}",
                accion, archivoId, ip ?? "unknown");
        }
        catch (Exception ex)
        {
            // Log errors should not fail the main operation
            _logger.LogError(ex, "Failed to log access for file {Id}", archivoId);
            // Don't rethrow - access logging failure should not break functionality
        }
    }

    public async Task<Archivo> CreateAsync(Archivo archivo)
    {
        try
        {
            _context.Archivos.Add(archivo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("File metadata created: {Id}, Name: {Name}",
                archivo.Id, archivo.NombreOriginal);

            return archivo;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            _logger.LogWarning(ex, "Duplicate file creation attempted: {Id}", archivo.Id);
            throw new ValidationException("El archivo ya existe");
        }
        catch (Exception ex) when (ex is not ShareSafelyException)
        {
            _logger.LogError(ex, "Database error creating file: {Id}", archivo.Id);
            throw new DatabaseException($"Error creating file {archivo.Id}", ex);
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var archivo = await _context.Archivos.FindAsync(id);
            if (archivo == null)
            {
                _logger.LogWarning("Attempted to delete non-existent file: {Id}", id);
                return false;
            }

            _context.Archivos.Remove(archivo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("File metadata deleted: {Id}", id);
            return true;
        }
        catch (Exception ex) when (ex is not ShareSafelyException)
        {
            _logger.LogError(ex, "Database error deleting file: {Id}", id);
            throw new DatabaseException($"Error deleting file {id}", ex);
        }
    }
}
