using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ShareSafely.Functions.Functions;

public class CleanupExpiredFiles
{
    private readonly ILogger<CleanupExpiredFiles> _logger;
    private readonly IConfiguration _config;

    public CleanupExpiredFiles(
        ILogger<CleanupExpiredFiles> logger,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Se ejecuta cada hora para limpiar archivos expirados
    /// Cron: "0 0 * * * *" = cada hora en punto
    /// </summary>
    [Function("CleanupExpiredFiles")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        _logger.LogInformation("Iniciando limpieza de archivos expirados: {Time}", DateTime.UtcNow);

        try
        {
            var expiredFiles = await GetExpiredFilesAsync();
            _logger.LogInformation("Archivos expirados encontrados: {Count}", expiredFiles.Count);

            if (expiredFiles.Count == 0)
            {
                _logger.LogInformation("No hay archivos para limpiar");
                return;
            }

            var blobServiceClient = new BlobServiceClient(_config["AzureStorage:ConnectionString"]);
            var containerClient = blobServiceClient.GetBlobContainerClient(_config["AzureStorage:ContainerName"]);

            int deleted = 0;
            int errors = 0;

            foreach (var file in expiredFiles)
            {
                try
                {
                    // Eliminar blob
                    var blobClient = containerClient.GetBlobClient(file.BlobName);
                    await blobClient.DeleteIfExistsAsync();

                    // Actualizar estado en BD
                    await UpdateFileStatusAsync(file.Id, 3); // 3 = Eliminado

                    deleted++;
                    _logger.LogInformation("Archivo eliminado: {Id} - {Name}", file.Id, file.BlobName);
                }
                catch (Exception ex)
                {
                    errors++;
                    _logger.LogError(ex, "Error al eliminar archivo: {Id}", file.Id);
                }
            }

            _logger.LogInformation(
                "Limpieza completada. Eliminados: {Deleted}, Errores: {Errors}",
                deleted, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en la limpieza de archivos");
            throw;
        }
    }

    private async Task<List<ExpiredFile>> GetExpiredFilesAsync()
    {
        var files = new List<ExpiredFile>();
        var connectionString = _config["ConnectionStrings:DefaultConnection"];

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = @"
            SELECT Id, Nombre, ContentType
            FROM Archivos
            WHERE Estado = 1
              AND FechaExpiracion IS NOT NULL
              AND FechaExpiracion < @Now";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            files.Add(new ExpiredFile
            {
                Id = reader.GetGuid(0),
                BlobName = reader.GetString(1),
                ContentType = reader.GetString(2)
            });
        }

        return files;
    }

    private async Task UpdateFileStatusAsync(Guid id, int status)
    {
        var connectionString = _config["ConnectionStrings:DefaultConnection"];

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "UPDATE Archivos SET Estado = @Status WHERE Id = @Id";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@Id", id);

        await command.ExecuteNonQueryAsync();
    }

    private class ExpiredFile
    {
        public Guid Id { get; set; }
        public string BlobName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
