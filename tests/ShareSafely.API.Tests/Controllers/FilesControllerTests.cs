using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ShareSafely.API.Controllers;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Services.Interfaces;
using ShareSafely.API.Tests.Helpers;

namespace ShareSafely.API.Tests.Controllers;

/// <summary>
/// Tests para FilesController
/// </summary>
public class FilesControllerTests
{
    private readonly Mock<IFileStorageService> _storageServiceMock;
    private readonly Mock<IFileMetadataService> _metadataServiceMock;
    private readonly Mock<ILogger<FilesController>> _loggerMock;
    private readonly FilesController _controller;

    public FilesControllerTests()
    {
        _storageServiceMock = new Mock<IFileStorageService>();
        _metadataServiceMock = new Mock<IFileMetadataService>();
        _loggerMock = new Mock<ILogger<FilesController>>();

        _controller = new FilesController(
            _storageServiceMock.Object,
            _metadataServiceMock.Object,
            _loggerMock.Object);
    }

    // ============================================================
    // Tests de Upload
    // ============================================================

    [Fact]
    public async Task Upload_WithValidFile_ShouldReturnOk()
    {
        // Arrange
        var request = TestDataFactory.CreateFileUploadRequest();
        var expectedResponse = new FileResponse
        {
            Id = Guid.NewGuid(),
            Nombre = "document.pdf",
            ContentType = "application/pdf",
            Tamanio = 1024,
            FechaSubida = DateTime.UtcNow,
            Estado = "Activo"
        };

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<FileUploadRequest>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.Upload(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<FileResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data!.Id.Should().Be(expectedResponse.Id);
    }

    [Fact]
    public async Task Upload_WhenServiceThrows_ShouldReturnBadRequest()
    {
        // Arrange
        var request = TestDataFactory.CreateFileUploadRequest();

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<FileUploadRequest>()))
            .ThrowsAsync(new InvalidOperationException("Archivo inválido"));

        // Act
        var result = await _controller.Upload(request);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var apiResponse = badRequest.Value.Should().BeOfType<ApiResponse<FileResponse>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("inválido");
    }

    // ============================================================
    // Tests de GetById
    // ============================================================

    [Fact]
    public async Task GetById_WithExistingFile_ShouldReturnOk()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var expectedResponse = new FileResponse
        {
            Id = fileId,
            Nombre = "test.pdf",
            Estado = "Activo"
        };

        _metadataServiceMock
            .Setup(s => s.GetByIdAsync(fileId))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetById(fileId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<FileResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data!.Id.Should().Be(fileId);
    }

    [Fact]
    public async Task GetById_WithNonExistingFile_ShouldReturnNotFound()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _metadataServiceMock
            .Setup(s => s.GetByIdAsync(fileId))
            .ReturnsAsync((FileResponse?)null);

        // Act
        var result = await _controller.GetById(fileId);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var apiResponse = notFound.Value.Should().BeOfType<ApiResponse<FileResponse>>().Subject;
        apiResponse.Success.Should().BeFalse();
    }

    // ============================================================
    // Tests de Delete
    // ============================================================

    [Fact]
    public async Task Delete_WithExistingFile_ShouldReturnOk()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _storageServiceMock
            .Setup(s => s.DeleteAsync(fileId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(fileId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        apiResponse.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistingFile_ShouldReturnNotFound()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _storageServiceMock
            .Setup(s => s.DeleteAsync(fileId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Delete(fileId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}
