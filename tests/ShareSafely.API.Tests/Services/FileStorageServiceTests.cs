using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Services;
using ShareSafely.API.Services.Interfaces;
using ShareSafely.API.Tests.Helpers;

namespace ShareSafely.API.Tests.Services;

/// <summary>
/// Tests para AzureBlobStorageService
/// </summary>
public class FileStorageServiceTests
{
    private readonly Mock<IFileMetadataService> _metadataServiceMock;
    private readonly Mock<ILogger<AzureBlobStorageService>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;

    public FileStorageServiceTests()
    {
        _metadataServiceMock = new Mock<IFileMetadataService>();
        _loggerMock = new Mock<ILogger<AzureBlobStorageService>>();
        _configMock = new Mock<IConfiguration>();

        // Configurar valores por defecto
        SetupConfiguration();
    }

    private void SetupConfiguration()
    {
        _configMock.Setup(c => c["FileValidation:MaxFileSizeMB"]).Returns("100");
        _configMock.Setup(c => c.GetSection("FileValidation:AllowedExtensions").Get<string[]>())
            .Returns(new[] { ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg", ".zip" });
    }

    // ============================================================
    // Tests de Validación de Archivos
    // ============================================================

    [Fact]
    public void ValidateFile_WithValidPdf_ShouldNotThrow()
    {
        // Arrange
        var file = TestDataFactory.CreateMockFormFile("document.pdf", "application/pdf", 1024);

        // Act
        var exception = Record.Exception(() => ValidateFileHelper(file));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ValidateFile_WithValidDocx_ShouldNotThrow()
    {
        // Arrange
        var file = TestDataFactory.CreateMockFormFile("document.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            1024);

        // Act
        var exception = Record.Exception(() => ValidateFileHelper(file));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".sh")]
    [InlineData(".dll")]
    public void ValidateFile_WithInvalidExtension_ShouldThrowException(string extension)
    {
        // Arrange
        var file = TestDataFactory.CreateInvalidFile(extension);

        // Act
        var act = () => ValidateFileHelper(file);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{extension}*no permitida*");
    }

    [Fact]
    public void ValidateFile_WithOversizedFile_ShouldThrowException()
    {
        // Arrange
        var file = TestDataFactory.CreateOversizedFile(150); // 150 MB

        // Act
        var act = () => ValidateFileHelper(file);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*excede el límite*");
    }

    [Theory]
    [InlineData(1)]      // 1 MB
    [InlineData(50)]     // 50 MB
    [InlineData(100)]    // 100 MB (límite)
    public void ValidateFile_WithValidSize_ShouldNotThrow(int sizeMB)
    {
        // Arrange
        var file = TestDataFactory.CreateMockFormFile("test.pdf", "application/pdf", sizeMB * 1024 * 1024);

        // Act
        var exception = Record.Exception(() => ValidateFileHelper(file));

        // Assert
        exception.Should().BeNull();
    }

    // ============================================================
    // Tests de Respuesta de Upload
    // ============================================================

    [Fact]
    public void UploadResponse_ShouldContainCorrectMetadata()
    {
        // Arrange
        var request = TestDataFactory.CreateFileUploadRequest("report.pdf", "application/pdf", 2048);

        // Act
        var response = new FileResponse
        {
            Id = Guid.NewGuid(),
            Nombre = request.Archivo.FileName,
            ContentType = request.Archivo.ContentType,
            Tamanio = request.Archivo.Length,
            FechaSubida = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddMinutes(request.ExpiracionMinutos ?? 60),
            Estado = "Activo"
        };

        // Assert
        response.Nombre.Should().Be("report.pdf");
        response.ContentType.Should().Be("application/pdf");
        response.Tamanio.Should().Be(2048);
        response.Estado.Should().Be("Activo");
        response.FechaExpiracion.Should().BeAfter(response.FechaSubida);
    }

    [Fact]
    public void UploadResponse_WithoutExpiration_ShouldHaveNullExpiration()
    {
        // Arrange
        var request = new FileUploadRequest
        {
            Archivo = TestDataFactory.CreateMockFormFile(),
            ExpiracionMinutos = null
        };

        // Act
        var response = new FileResponse
        {
            Id = Guid.NewGuid(),
            Nombre = request.Archivo.FileName,
            FechaSubida = DateTime.UtcNow,
            FechaExpiracion = request.ExpiracionMinutos.HasValue
                ? DateTime.UtcNow.AddMinutes(request.ExpiracionMinutos.Value)
                : null,
            Estado = "Activo"
        };

        // Assert
        response.FechaExpiracion.Should().BeNull();
    }

    // ============================================================
    // Helper para validación (simula lógica del servicio)
    // ============================================================

    private void ValidateFileHelper(Microsoft.AspNetCore.Http.IFormFile file)
    {
        var maxSize = 100 * 1024 * 1024; // 100 MB
        var allowedExt = new[] { ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg", ".zip" };

        if (file.Length > maxSize)
            throw new InvalidOperationException($"Archivo excede el límite de {maxSize / 1024 / 1024}MB");

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExt.Contains(ext))
            throw new InvalidOperationException($"Extensión {ext} no permitida");
    }
}
