using Microsoft.AspNetCore.Mvc;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Services.Interfaces;

namespace ShareSafely.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LinksController : ControllerBase
{
    private readonly ISasLinkService _linkService;
    private readonly ILogger<LinksController> _logger;

    public LinksController(
        ISasLinkService linkService,
        ILogger<LinksController> logger)
    {
        _linkService = linkService;
        _logger = logger;
    }

    /// <summary>
    /// Genera un enlace SAS para compartir un archivo
    /// POST /api/links/generate
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<LinkResponse>>> Generate(
        [FromBody] LinkGenerateRequest request)
    {
        try
        {
            var result = await _linkService.GenerateAsync(request);
            _logger.LogInformation("Enlace generado para archivo: {Id}", request.ArchivoId);

            return Ok(ApiResponse<LinkResponse>.Ok(result, "Enlace generado correctamente"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar enlace");
            return BadRequest(ApiResponse<LinkResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Descarga un archivo usando el token SAS
    /// GET /api/links/download/{token}
    /// </summary>
    [HttpGet("download/{token}")]
    public async Task<IActionResult> Download(string token)
    {
        var isValid = await _linkService.ValidateAsync(token);

        if (!isValid)
        {
            _logger.LogWarning("Intento de descarga con token inválido: {Token}", token);
            return NotFound(ApiResponse<string>.Fail("Enlace inválido o expirado"));
        }

        var downloadUrl = await _linkService.GetDownloadUrlAsync(token);

        if (string.IsNullOrEmpty(downloadUrl))
            return NotFound(ApiResponse<string>.Fail("Archivo no encontrado"));

        _logger.LogInformation("Descarga iniciada con token: {Token}", token);
        return Redirect(downloadUrl);
    }

    /// <summary>
    /// Revoca un enlace antes de su expiración
    /// DELETE /api/links/{id}
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Revoke(Guid id)
    {
        var revoked = await _linkService.RevokeAsync(id);

        if (!revoked)
            return NotFound(ApiResponse<bool>.Fail("Enlace no encontrado"));

        _logger.LogInformation("Enlace revocado: {Id}", id);
        return Ok(ApiResponse<bool>.Ok(true, "Enlace revocado correctamente"));
    }
}
