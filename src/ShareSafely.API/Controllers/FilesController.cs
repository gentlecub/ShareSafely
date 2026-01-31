using Microsoft.AspNetCore.Mvc;
using ShareSafely.API.Exceptions;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storageService;
    private readonly IFileMetadataService _metadataService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileStorageService storageService,
        IFileMetadataService metadataService,
        ILogger<FilesController> logger)
    {
        _storageService = storageService;
        _metadataService = metadataService;
        _logger = logger;
    }

    /// <summary>
    /// Sube un archivo a Azure Blob Storage
    /// POST /api/files/upload
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(104857600)] // 100 MB
    [ProducesResponseType(typeof(ApiResponse<FileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FileResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<FileResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<FileResponse>>> Upload(
        [FromForm] FileUploadRequest request)
    {
        // Services throw ValidationException or StorageException
        // GlobalExceptionMiddleware handles all errors
        var result = await _storageService.UploadAsync(request);

        // Save metadata to database
        var archivo = new Archivo
        {
            Id = result.Id,
            Nombre = $"{result.Id}{Path.GetExtension(result.Nombre)}",
            NombreOriginal = result.Nombre,
            ContentType = result.ContentType,
            Tamanio = result.Tamanio,
            FechaSubida = result.FechaSubida,
            FechaExpiracion = result.FechaExpiracion,
            Estado = EstadoArchivo.Activo,
            BlobUrl = $"{result.Id}{Path.GetExtension(result.Nombre)}"
        };

        await _metadataService.CreateAsync(archivo);

        // Log access
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _metadataService.LogAccessAsync(result.Id, TipoAccion.Subida, ip);

        _logger.LogInformation("File uploaded successfully: {Id}, Name: {Name}, Size: {Size}",
            result.Id, result.Nombre, result.Tamanio);

        return Ok(ApiResponse<FileResponse>.Ok(result, "Archivo subido correctamente"));
    }

    /// <summary>
    /// Obtiene información de un archivo
    /// GET /api/files/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FileResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FileResponse>>> GetById(Guid id)
    {
        var file = await _metadataService.GetByIdAsync(id);

        if (file == null)
        {
            _logger.LogWarning("File not found: {Id}", id);
            throw new NotFoundException("Archivo", id);
        }

        return Ok(ApiResponse<FileResponse>.Ok(file));
    }

    /// <summary>
    /// Elimina un archivo
    /// DELETE /api/files/{id}
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id)
    {
        var deleted = await _storageService.DeleteAsync(id);

        if (!deleted)
        {
            _logger.LogWarning("Delete failed - file not found: {Id}", id);
            throw new NotFoundException("Archivo", id);
        }

        // Log access
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _metadataService.LogAccessAsync(id, TipoAccion.ArchivoEliminado, ip);

        _logger.LogInformation("File deleted: {Id}", id);
        return Ok(ApiResponse<bool>.Ok(true, "Archivo eliminado correctamente"));
    }

    /// <summary>
    /// Lista archivos con paginación
    /// GET /api/files?page=1&pageSize=10
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<FileResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<FileResponse>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        // This would need a new method in the service
        // For now, return empty list with proper structure
        _logger.LogInformation("Listing files: page={Page}, pageSize={PageSize}", page, pageSize);

        return Ok(ApiResponse<List<FileResponse>>.Ok(
            new List<FileResponse>(),
            "Lista de archivos"));
    }
}
