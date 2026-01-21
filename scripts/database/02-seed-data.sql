-- ============================================================
-- ShareSafely - Datos de Prueba (Opcional)
-- ============================================================
-- Ejecutar después de 01-create-tables.sql
-- ============================================================

-- Insertar archivo de prueba
DECLARE @ArchivoId UNIQUEIDENTIFIER = NEWID();

INSERT INTO Archivos (Id, Nombre, NombreOriginal, ContentType, Tamanio, FechaSubida, FechaExpiracion, Estado, BlobUrl)
VALUES (
    @ArchivoId,
    CONCAT(CAST(@ArchivoId AS NVARCHAR(36)), '.pdf'),
    'documento-prueba.pdf',
    'application/pdf',
    1048576,  -- 1 MB
    GETUTCDATE(),
    DATEADD(HOUR, 24, GETUTCDATE()),
    1,  -- Activo
    'https://sharesafelystorage.blob.core.windows.net/archivos/'
);

-- Insertar enlace de prueba
INSERT INTO Enlaces (ArchivoId, Token, UrlCompleta, FechaCreacion, FechaExpiracion, Estado)
VALUES (
    @ArchivoId,
    REPLACE(NEWID(), '-', ''),
    'https://sharesafelystorage.blob.core.windows.net/archivos/...',
    GETUTCDATE(),
    DATEADD(HOUR, 1, GETUTCDATE()),
    1  -- Activo
);

-- Insertar log de prueba
INSERT INTO LogsAcceso (ArchivoId, Accion, Timestamp, IpAddress)
VALUES (
    @ArchivoId,
    1,  -- Subida
    GETUTCDATE(),
    '127.0.0.1'
);

-- Verificar datos
SELECT 'Archivos' AS Tabla, COUNT(*) AS Total FROM Archivos
UNION ALL
SELECT 'Enlaces', COUNT(*) FROM Enlaces
UNION ALL
SELECT 'LogsAcceso', COUNT(*) FROM LogsAcceso;

PRINT 'Datos de prueba insertados';
