# ============================================================
# ShareSafely API - Dockerfile (Multi-stage build)
# ============================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo el archivo de proyecto primero (mejor cache)
COPY src/ShareSafely.API/ShareSafely.API.csproj ./ShareSafely.API/
RUN dotnet restore ./ShareSafely.API/ShareSafely.API.csproj

# Copiar el resto del código
COPY src/ShareSafely.API/ ./ShareSafely.API/

# Build y publish
WORKDIR /src/ShareSafely.API
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime (imagen liviana)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Crear usuario no-root para seguridad
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

# Copiar artefactos del build
COPY --from=build /app/publish .

# Cambiar a usuario no-root
USER appuser

# Variables de entorno (se sobreescriben en docker-compose)
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Development

# Puerto expuesto
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5000/health || exit 1

# Comando de inicio
ENTRYPOINT ["dotnet", "ShareSafely.API.dll"]
