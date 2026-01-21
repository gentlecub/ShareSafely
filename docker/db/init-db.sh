#!/bin/bash
# ============================================================
# Script de inicialización de SQL Server
# ============================================================

echo "Esperando que SQL Server inicie..."
sleep 30

echo "Creando base de datos ShareSafely..."

/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -Q "
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ShareSafelyDB')
BEGIN
    CREATE DATABASE ShareSafelyDB;
    PRINT 'Base de datos ShareSafelyDB creada';
END
ELSE
    PRINT 'Base de datos ShareSafelyDB ya existe';
"

echo "Ejecutando script de tablas..."

/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -d ShareSafelyDB -i /docker-entrypoint-initdb.d/01-create-tables.sql

echo "Inicialización completada"
