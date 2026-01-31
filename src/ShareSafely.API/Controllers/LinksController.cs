using Microsoft.AspNetCore.Mvc;
using ShareSafely.API.Exceptions;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LinksController : ControllerBase
{
    private readonly ISasLinkService _linkService;
    private readonly IFileMetadataService _metadataService;
    private readonly IFileStorageService _storageService;
    private readonly ILogger<LinksController> _logger;

    public LinksController(
        ISasLinkService linkService,
        IFileMetadataService metadataService,
        IFileStorageService storageService,
        ILogger<LinksController> logger)
    {
        _linkService = linkService;
        _metadataService = metadataService;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Genera un enlace SAS para compartir un archivo
    /// POST /api/links/generate
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ApiResponse<LinkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LinkResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LinkResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<LinkResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<LinkResponse>>> Generate(
        [FromBody] LinkGenerateRequest request)
    {
        // Service throws NotFoundException, ValidationException, or StorageException
        // GlobalExceptionMiddleware handles all errors
        var result = await _linkService.GenerateAsync(request);

        // Log access
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _metadataService.LogAccessAsync(request.ArchivoId, TipoAccion.EnlaceGenerado, ip);

        _logger.LogInformation("Link generated for file: {FileId}, Link: {LinkId}, Expires: {Expiration}",
            request.ArchivoId, result.Id, result.FechaExpiracion);

        return Ok(ApiResponse<LinkResponse>.Ok(result, "Enlace generado correctamente"));
    }

    /// <summary>
    /// Descarga un archivo usando el token SAS
    /// GET /api/links/download/{token}
    /// </summary>
    [HttpGet("download/{token}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Download(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Download attempted with empty token");
            throw new ValidationException("Token es requerido");
        }

        var isValid = await _linkService.ValidateAsync(token);

        if (!isValid)
        {
            _logger.LogWarning("Download attempted with invalid/expired token: {Token}",
                token.Length > 8 ? $"{token[..8]}..." : token);
            throw new LinkExpiredException(token);
        }

        var downloadUrl = await _linkService.GetDownloadUrlAsync(token);

        if (string.IsNullOrEmpty(downloadUrl))
        {
            _logger.LogWarning("Download URL not found for token: {Token}",
                token.Length > 8 ? $"{token[..8]}..." : token);
            throw new NotFoundException("Enlace");
        }

        // Handle local storage downloads
        if (downloadUrl.StartsWith("LOCAL:"))
        {
            var archivoIdStr = downloadUrl.Replace("LOCAL:", "");
            if (Guid.TryParse(archivoIdStr, out var archivoId))
            {
                var archivo = await _metadataService.GetByIdAsync(archivoId);
                if (archivo == null)
                {
                    throw new NotFoundException("Archivo", archivoId);
                }

                var stream = await _storageService.DownloadAsync(archivoId);
                if (stream == null)
                {
                    throw new NotFoundException("Archivo", archivoId);
                }

                _logger.LogInformation("Local download for token: {Token}, File: {FileName}",
                    token.Length > 8 ? $"{token[..8]}..." : token, archivo.Nombre);

                return File(stream, archivo.ContentType ?? "application/octet-stream", archivo.Nombre);
            }
        }

        _logger.LogInformation("Download redirect for token: {Token}",
            token.Length > 8 ? $"{token[..8]}..." : token);

        return Redirect(downloadUrl);
    }

    /// <summary>
    /// Valida si un token es válido sin descargarlo
    /// GET /api/links/validate/{token}
    /// </summary>
    [HttpGet("validate/{token}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Ok(ApiResponse<bool>.Ok(false, "Token inválido"));
        }

        var isValid = await _linkService.ValidateAsync(token);

        return Ok(ApiResponse<bool>.Ok(isValid, isValid ? "Token válido" : "Token inválido o expirado"));
    }

    /// <summary>
    /// Revoca un enlace antes de su expiración
    /// DELETE /api/links/{id}
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> Revoke(Guid id)
    {
        var revoked = await _linkService.RevokeAsync(id);

        if (!revoked)
        {
            _logger.LogWarning("Revoke failed - link not found: {Id}", id);
            throw new NotFoundException("Enlace", id);
        }

        _logger.LogInformation("Link revoked: {Id}", id);
        return Ok(ApiResponse<bool>.Ok(true, "Enlace revocado correctamente"));
    }
}
