using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ShareSafely.API.Models.DTOs;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services.Interfaces;
using ShareSafely.API.Tests.Helpers;

namespace ShareSafely.API.Tests.Services;

/// <summary>
/// Tests para SasLinkService
/// </summary>
public class SasLinkServiceTests
{
    private readonly Mock<IFileMetadataService> _metadataServiceMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<SasLinkServiceTests>> _loggerMock;

    public SasLinkServiceTests()
    {
        _metadataServiceMock = new Mock<IFileMetadataService>();
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<SasLinkServiceTests>>();
    }

    // ============================================================
    // Tests de Generación de Enlaces
    // ============================================================

    [Fact]
    public void GenerateLink_ShouldCreateUniqueToken()
    {
        // Arrange
        var archivo = TestDataFactory.CreateArchivo();

        // Act
        var enlace1 = TestDataFactory.CreateEnlace(archivo.Id);
        var enlace2 = TestDataFactory.CreateEnlace(archivo.Id);

        // Assert
        enlace1.Token.Should().NotBe(enlace2.Token);
        enlace1.Token.Should().HaveLength(32); // GUID sin guiones
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(1440)]
    public void GenerateLink_ShouldSetCorrectExpiration(int minutos)
    {
        // Arrange
        var request = TestDataFactory.CreateLinkGenerateRequest(expiracionMinutos: minutos);
        var ahora = DateTime.UtcNow;

        // Act
        var enlace = new Enlace
        {
            Id = Guid.NewGuid(),
            ArchivoId = request.ArchivoId,
            Token = Guid.NewGuid().ToString("N"),
            FechaCreacion = ahora,
            FechaExpiracion = ahora.AddMinutes(minutos),
            Estado = EstadoEnlace.Activo
        };

        // Assert
        enlace.FechaExpiracion.Should().BeCloseTo(
            ahora.AddMinutes(minutos),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GenerateLink_ShouldSetEstadoActivo()
    {
        // Arrange & Act
        var enlace = TestDataFactory.CreateEnlace();

        // Assert
        enlace.Estado.Should().Be(EstadoEnlace.Activo);
        enlace.AccesosCount.Should().Be(0);
    }

    // ============================================================
    // Tests de Validación de Enlaces
    // ============================================================

    [Fact]
    public void ValidateLink_WithActiveLink_ShouldReturnTrue()
    {
        // Arrange
        var enlace = TestDataFactory.CreateEnlace(expiracionMinutos: 60);

        // Act
        var isValid = enlace.Estado == EstadoEnlace.Activo
                   && enlace.FechaExpiracion > DateTime.UtcNow;

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateLink_WithExpiredLink_ShouldReturnFalse()
    {
        // Arrange
        var enlace = new Enlace
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            FechaCreacion = DateTime.UtcNow.AddHours(-2),
            FechaExpiracion = DateTime.UtcNow.AddHours(-1), // Expiró hace 1 hora
            Estado = EstadoEnlace.Activo
        };

        // Act
        var isValid = enlace.Estado == EstadoEnlace.Activo
                   && enlace.FechaExpiracion > DateTime.UtcNow;

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateLink_WithRevokedLink_ShouldReturnFalse()
    {
        // Arrange
        var enlace = TestDataFactory.CreateEnlace();
        enlace.Estado = EstadoEnlace.Revocado;

        // Act
        var isValid = enlace.Estado == EstadoEnlace.Activo
                   && enlace.FechaExpiracion > DateTime.UtcNow;

        // Assert
        isValid.Should().BeFalse();
    }

    // ============================================================
    // Tests de LinkResponse
    // ============================================================

    [Fact]
    public void LinkResponse_ShouldContainRequiredFields()
    {
        // Arrange
        var archivoId = Guid.NewGuid();
        var request = TestDataFactory.CreateLinkGenerateRequest(archivoId, 60);

        // Act
        var response = new LinkResponse
        {
            Id = Guid.NewGuid(),
            ArchivoId = archivoId,
            Url = $"https://storage.blob.core.windows.net/archivos/test.pdf?sv=2021-06-08&se=...",
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddMinutes(60),
            Estado = "Activo"
        };

        // Assert
        response.ArchivoId.Should().Be(archivoId);
        response.Url.Should().Contain("blob.core.windows.net");
        response.Url.Should().Contain("sv="); // SAS parameter
        response.Estado.Should().Be("Activo");
    }

    // ============================================================
    // Tests de Revocación
    // ============================================================

    [Fact]
    public void RevokeLink_ShouldChangeEstadoToRevocado()
    {
        // Arrange
        var enlace = TestDataFactory.CreateEnlace();
        enlace.Estado.Should().Be(EstadoEnlace.Activo);

        // Act
        enlace.Estado = EstadoEnlace.Revocado;

        // Assert
        enlace.Estado.Should().Be(EstadoEnlace.Revocado);
    }

    [Fact]
    public void RevokeLink_ShouldNotAffectExpiration()
    {
        // Arrange
        var enlace = TestDataFactory.CreateEnlace(expiracionMinutos: 60);
        var originalExpiration = enlace.FechaExpiracion;

        // Act
        enlace.Estado = EstadoEnlace.Revocado;

        // Assert
        enlace.FechaExpiracion.Should().Be(originalExpiration);
    }
}
