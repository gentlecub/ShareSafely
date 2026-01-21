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
/// Tests para LinksController
/// </summary>
public class LinksControllerTests
{
    private readonly Mock<ISasLinkService> _linkServiceMock;
    private readonly Mock<ILogger<LinksController>> _loggerMock;
    private readonly LinksController _controller;

    public LinksControllerTests()
    {
        _linkServiceMock = new Mock<ISasLinkService>();
        _loggerMock = new Mock<ILogger<LinksController>>();

        _controller = new LinksController(
            _linkServiceMock.Object,
            _loggerMock.Object);
    }

    // ============================================================
    // Tests de Generate
    // ============================================================

    [Fact]
    public async Task Generate_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = TestDataFactory.CreateLinkGenerateRequest();
        var expectedResponse = new LinkResponse
        {
            Id = Guid.NewGuid(),
            ArchivoId = request.ArchivoId,
            Url = "https://storage.blob.core.windows.net/archivos/test?sv=...",
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddMinutes(60),
            Estado = "Activo"
        };

        _linkServiceMock
            .Setup(s => s.GenerateAsync(It.IsAny<LinkGenerateRequest>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.Generate(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<LinkResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data!.Url.Should().Contain("blob.core.windows.net");
    }

    [Fact]
    public async Task Generate_WhenFileNotFound_ShouldReturnBadRequest()
    {
        // Arrange
        var request = TestDataFactory.CreateLinkGenerateRequest();

        _linkServiceMock
            .Setup(s => s.GenerateAsync(It.IsAny<LinkGenerateRequest>()))
            .ThrowsAsync(new InvalidOperationException("Archivo no encontrado"));

        // Act
        var result = await _controller.Generate(request);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var apiResponse = badRequest.Value.Should().BeOfType<ApiResponse<LinkResponse>>().Subject;
        apiResponse.Success.Should().BeFalse();
    }

    // ============================================================
    // Tests de Download
    // ============================================================

    [Fact]
    public async Task Download_WithValidToken_ShouldRedirect()
    {
        // Arrange
        var token = Guid.NewGuid().ToString("N");
        var downloadUrl = "https://storage.blob.core.windows.net/archivos/test.pdf?sv=...";

        _linkServiceMock
            .Setup(s => s.ValidateAsync(token))
            .ReturnsAsync(true);

        _linkServiceMock
            .Setup(s => s.GetDownloadUrlAsync(token))
            .ReturnsAsync(downloadUrl);

        // Act
        var result = await _controller.Download(token);

        // Assert
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(downloadUrl);
    }

    [Fact]
    public async Task Download_WithInvalidToken_ShouldReturnNotFound()
    {
        // Arrange
        var token = "invalid-token";

        _linkServiceMock
            .Setup(s => s.ValidateAsync(token))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Download(token);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Download_WithExpiredToken_ShouldReturnNotFound()
    {
        // Arrange
        var token = Guid.NewGuid().ToString("N");

        _linkServiceMock
            .Setup(s => s.ValidateAsync(token))
            .ReturnsAsync(false); // Token expirado = inválido

        // Act
        var result = await _controller.Download(token);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ============================================================
    // Tests de Revoke
    // ============================================================

    [Fact]
    public async Task Revoke_WithExistingLink_ShouldReturnOk()
    {
        // Arrange
        var linkId = Guid.NewGuid();

        _linkServiceMock
            .Setup(s => s.RevokeAsync(linkId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Revoke(linkId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        apiResponse.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Revoke_WithNonExistingLink_ShouldReturnNotFound()
    {
        // Arrange
        var linkId = Guid.NewGuid();

        _linkServiceMock
            .Setup(s => s.RevokeAsync(linkId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Revoke(linkId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}
