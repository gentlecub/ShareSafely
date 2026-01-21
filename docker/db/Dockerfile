# ============================================================
# ShareSafely Database - SQL Server 2022
# ============================================================

FROM mcr.microsoft.com/mssql/server:2022-latest

# Variables de entorno (se sobreescriben en docker-compose)
ENV ACCEPT_EULA=Y
ENV MSSQL_PID=Express

# Copiar scripts de inicialización
COPY docker/db/init-db.sh /usr/local/bin/
COPY scripts/database/01-create-tables.sql /docker-entrypoint-initdb.d/

# Dar permisos de ejecución
USER root
RUN chmod +x /usr/local/bin/init-db.sh

# Volver a usuario mssql
USER mssql

# Puerto expuesto
EXPOSE 1433

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=5 \
    CMD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -Q "SELECT 1" || exit 1
