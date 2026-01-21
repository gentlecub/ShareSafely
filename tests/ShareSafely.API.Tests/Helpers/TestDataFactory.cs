using Microsoft.AspNetCore.Http;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;

namespace ShareSafely.API.Tests.Helpers;

/// <summary>
/// Factory para crear datos de prueba consistentes
/// </summary>
public static class TestDataFactory
{
    // ============================================================
    // Archivos
    // ============================================================

    public static Archivo CreateArchivo(
        Guid? id = null,
        string nombre = "test-file.pdf",
        EstadoArchivo estado = EstadoArchivo.Activo)
    {
        return new Archivo
        {
            Id = id ?? Guid.NewGuid(),
            Nombre = $"{Guid.NewGuid()}.pdf",
            NombreOriginal = nombre,
            ContentType = "application/pdf",
            Tamanio = 1024 * 100, // 100 KB
            FechaSubida = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddHours(1),
            Estado = estado,
            BlobUrl = "https://storage.blob.core.windows.net/archivos/test.pdf"
        };
    }

    public static List<Archivo> CreateArchivos(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateArchivo(nombre: $"file-{i}.pdf"))
            .ToList();
    }

    // ============================================================
    // Enlaces
    // ============================================================

    public static Enlace CreateEnlace(
        Guid? archivoId = null,
        int expiracionMinutos = 60,
        EstadoEnlace estado = EstadoEnlace.Activo)
    {
        return new Enlace
        {
            Id = Guid.NewGuid(),
            ArchivoId = archivoId ?? Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            UrlCompleta = "https://storage.blob.core.windows.net/archivos/test.pdf?sv=...",
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddMinutes(expiracionMinutos),
            Estado = estado,
            AccesosCount = 0
        };
    }

    // ============================================================
    // DTOs
    // ============================================================

    public static FileUploadRequest CreateFileUploadRequest(
        string fileName = "document.pdf",
        string contentType = "application/pdf",
        long size = 1024 * 100)
    {
        var fileMock = CreateMockFormFile(fileName, contentType, size);
        return new FileUploadRequest
        {
            Archivo = fileMock,
            ExpiracionMinutos = 60
        };
    }

    public static LinkGenerateRequest CreateLinkGenerateRequest(
        Guid? archivoId = null,
        int expiracionMinutos = 60)
    {
        return new LinkGenerateRequest
        {
            ArchivoId = archivoId ?? Guid.NewGuid(),
            ExpiracionMinutos = expiracionMinutos
        };
    }

    // ============================================================
    // Mock de IFormFile
    // ============================================================

    public static IFormFile CreateMockFormFile(
        string fileName = "test.pdf",
        string contentType = "application/pdf",
        long size = 1024)
    {
        var content = new byte[size];
        var stream = new MemoryStream(content);

        return new FormFile(stream, 0, size, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public static IFormFile CreateInvalidFile(string extension = ".exe")
    {
        return CreateMockFormFile($"malware{extension}", "application/octet-stream", 1024);
    }

    public static IFormFile CreateOversizedFile(long sizeMB = 150)
    {
        return CreateMockFormFile("large.pdf", "application/pdf", sizeMB * 1024 * 1024);
    }
}
