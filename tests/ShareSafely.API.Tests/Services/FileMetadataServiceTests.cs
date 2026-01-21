using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ShareSafely.API.Data;
using ShareSafely.API.Models.Entities;
using ShareSafely.API.Services;
using ShareSafely.API.Tests.Helpers;

namespace ShareSafely.API.Tests.Services;

/// <summary>
/// Tests para FileMetadataService usando EF InMemory
/// </summary>
public class FileMetadataServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly FileMetadataService _service;
    private readonly Mock<ILogger<FileMetadataService>> _loggerMock;

    public FileMetadataServiceTests()
    {
        // Crear DbContext InMemory con nombre único por test
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<FileMetadataService>>();
        _service = new FileMetadataService(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ============================================================
    // Tests de GetByIdAsync
    // ============================================================

    [Fact]
    public async Task GetByIdAsync_WithExistingFile_ShouldReturnFileResponse()
    {
        // Arrange
        var archivo = TestDataFactory.CreateArchivo();
        _context.Archivos.Add(archivo);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(archivo.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(archivo.Id);
        result.Nombre.Should().Be(archivo.NombreOriginal);
        result.ContentType.Should().Be(archivo.ContentType);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingFile_ShouldReturnNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _service.GetByIdAsync(nonExistingId);

        // Assert
        result.Should().BeNull();
    }

    // ============================================================
    // Tests de GetExpiredAsync
    // ============================================================

    [Fact]
    public async Task GetExpiredAsync_ShouldReturnOnlyExpiredFiles()
    {
        // Arrange
        var archivoActivo = TestDataFactory.CreateArchivo();
        archivoActivo.FechaExpiracion = DateTime.UtcNow.AddHours(1);
        archivoActivo.Estado = EstadoArchivo.Activo;

        var archivoExpirado = TestDataFactory.CreateArchivo();
        archivoExpirado.FechaExpiracion = DateTime.UtcNow.AddHours(-1);
        archivoExpirado.Estado = EstadoArchivo.Activo;

        var archivoYaEliminado = TestDataFactory.CreateArchivo();
        archivoYaEliminado.FechaExpiracion = DateTime.UtcNow.AddHours(-2);
        archivoYaEliminado.Estado = EstadoArchivo.Eliminado;

        _context.Archivos.AddRange(archivoActivo, archivoExpirado, archivoYaEliminado);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetExpiredAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(archivoExpirado.Id);
    }

    [Fact]
    public async Task GetExpiredAsync_WithNoExpiredFiles_ShouldReturnEmptyList()
    {
        // Arrange
        var archivo = TestDataFactory.CreateArchivo();
        archivo.FechaExpiracion = DateTime.UtcNow.AddHours(1);
        _context.Archivos.Add(archivo);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetExpiredAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ============================================================
    // Tests de UpdateStatusAsync
    // ============================================================

    [Fact]
    public async Task UpdateStatusAsync_ShouldChangeStatus()
    {
        // Arrange
        var archivo = TestDataFactory.CreateArchivo();
        archivo.Estado = EstadoArchivo.Activo;
        _context.Archivos.Add(archivo);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UpdateStatusAsync(archivo.Id, EstadoArchivo.Expirado);

        // Assert
        result.Should().BeTrue();
        var updated = await _context.Archivos.FindAsync(archivo.Id);
        updated!.Estado.Should().Be(EstadoArchivo.Expirado);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithNonExistingFile_ShouldReturnFalse()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _service.UpdateStatusAsync(nonExistingId, EstadoArchivo.Eliminado);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(EstadoArchivo.Activo, EstadoArchivo.Expirado)]
    [InlineData(EstadoArchivo.Activo, EstadoArchivo.Eliminado)]
    [InlineData(EstadoArchivo.Expirado, EstadoArchivo.Eliminado)]
    public async Task UpdateStatusAsync_ShouldHandleAllTransitions(
        EstadoArchivo inicial, EstadoArchivo final)
    {
        // Arrange
        var archivo = TestDataFactory.CreateArchivo();
        archivo.Estado = inicial;
        _context.Archivos.Add(archivo);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateStatusAsync(archivo.Id, final);

        // Assert
        var updated = await _context.Archivos.FindAsync(archivo.Id);
        updated!.Estado.Should().Be(final);
    }

    // ============================================================
    // Tests de LogAccessAsync
    // ============================================================

    [Fact]
    public async Task LogAccessAsync_ShouldCreateLogEntry()
    {
        // Arrange
        var archivo = TestDataFactory.CreateArchivo();
        _context.Archivos.Add(archivo);
        await _context.SaveChangesAsync();

        // Act
        await _service.LogAccessAsync(archivo.Id, TipoAccion.Descarga, "192.168.1.1");

        // Assert
        var log = await _context.LogsAcceso.FirstOrDefaultAsync(l => l.ArchivoId == archivo.Id);
        log.Should().NotBeNull();
        log!.Accion.Should().Be(TipoAccion.Descarga);
        log.IpAddress.Should().Be("192.168.1.1");
    }

    [Theory]
    [InlineData(TipoAccion.Subida)]
    [InlineData(TipoAccion.Descarga)]
    [InlineData(TipoAccion.EnlaceGenerado)]
    [InlineData(TipoAccion.ArchivoEliminado)]
    public async Task LogAccessAsync_ShouldLogAllActionTypes(TipoAccion accion)
    {
        // Arrange
        var archivo = TestDataFactory.CreateArchivo();
        _context.Archivos.Add(archivo);
        await _context.SaveChangesAsync();

        // Act
        await _service.LogAccessAsync(archivo.Id, accion);

        // Assert
        var log = await _context.LogsAcceso.FirstOrDefaultAsync(l => l.ArchivoId == archivo.Id);
        log!.Accion.Should().Be(accion);
    }
}
