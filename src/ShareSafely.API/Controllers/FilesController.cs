using Microsoft.AspNetCore.Mvc;
using ShareSafely.API.Models.DTOs;
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
    public async Task<ActionResult<ApiResponse<FileResponse>>> Upload(
        [FromForm] FileUploadRequest request)
    {
        try
        {
            var result = await _storageService.UploadAsync(request);
            _logger.LogInformation("Archivo subido: {Id}", result.Id);

            return Ok(ApiResponse<FileResponse>.Ok(result, "Archivo subido correctamente"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al subir archivo");
            return BadRequest(ApiResponse<FileResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Obtiene información de un archivo
    /// GET /api/files/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<FileResponse>>> GetById(Guid id)
    {
        var file = await _metadataService.GetByIdAsync(id);

        if (file == null)
            return NotFound(ApiResponse<FileResponse>.Fail("Archivo no encontrado"));

        return Ok(ApiResponse<FileResponse>.Ok(file));
    }

    /// <summary>
    /// Elimina un archivo
    /// DELETE /api/files/{id}
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id)
    {
        var deleted = await _storageService.DeleteAsync(id);

        if (!deleted)
            return NotFound(ApiResponse<bool>.Fail("Archivo no encontrado"));

        _logger.LogInformation("Archivo eliminado: {Id}", id);
        return Ok(ApiResponse<bool>.Ok(true, "Archivo eliminado correctamente"));
    }
}
