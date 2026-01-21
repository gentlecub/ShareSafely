-- ============================================================
-- ShareSafely - Script de Creación de Tablas
-- ============================================================
-- Ejecutar en Azure SQL Database o SQL Server
-- ============================================================

-- Tabla: Archivos
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Archivos')
BEGIN
    CREATE TABLE Archivos (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Nombre NVARCHAR(255) NOT NULL,
        NombreOriginal NVARCHAR(255) NOT NULL,
        ContentType NVARCHAR(100) NULL,
        Tamanio BIGINT NOT NULL,
        FechaSubida DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaExpiracion DATETIME2 NULL,
        Estado INT NOT NULL DEFAULT 1,
        BlobUrl NVARCHAR(500) NULL
    );

    CREATE INDEX IX_Archivos_Estado ON Archivos(Estado);
    CREATE INDEX IX_Archivos_FechaExpiracion ON Archivos(FechaExpiracion);

    PRINT 'Tabla Archivos creada';
END
ELSE
    PRINT 'Tabla Archivos ya existe';

-- Tabla: Enlaces
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Enlaces')
BEGIN
    CREATE TABLE Enlaces (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ArchivoId UNIQUEIDENTIFIER NOT NULL,
        Token NVARCHAR(100) NOT NULL,
        UrlCompleta NVARCHAR(1000) NULL,
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaExpiracion DATETIME2 NOT NULL,
        Estado INT NOT NULL DEFAULT 1,
        AccesosCount INT NOT NULL DEFAULT 0,

        CONSTRAINT FK_Enlaces_Archivos
            FOREIGN KEY (ArchivoId) REFERENCES Archivos(Id) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_Enlaces_Token ON Enlaces(Token);
    CREATE INDEX IX_Enlaces_Estado ON Enlaces(Estado);

    PRINT 'Tabla Enlaces creada';
END
ELSE
    PRINT 'Tabla Enlaces ya existe';

-- Tabla: LogsAcceso
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LogsAcceso')
BEGIN
    CREATE TABLE LogsAcceso (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ArchivoId UNIQUEIDENTIFIER NOT NULL,
        EnlaceId UNIQUEIDENTIFIER NULL,
        Accion INT NOT NULL,
        Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        IpAddress NVARCHAR(50) NULL,
        UserAgent NVARCHAR(500) NULL,

        CONSTRAINT FK_LogsAcceso_Archivos
            FOREIGN KEY (ArchivoId) REFERENCES Archivos(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_LogsAcceso_Timestamp ON LogsAcceso(Timestamp);
    CREATE INDEX IX_LogsAcceso_ArchivoId ON LogsAcceso(ArchivoId);

    PRINT 'Tabla LogsAcceso creada';
END
ELSE
    PRINT 'Tabla LogsAcceso ya existe';

-- ============================================================
-- Verificación
-- ============================================================
SELECT
    t.name AS Tabla,
    SUM(p.rows) AS Filas
FROM sys.tables t
JOIN sys.partitions p ON t.object_id = p.object_id
WHERE t.name IN ('Archivos', 'Enlaces', 'LogsAcceso')
  AND p.index_id IN (0, 1)
GROUP BY t.name;

PRINT '';
PRINT 'Script completado exitosamente';
