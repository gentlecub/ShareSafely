# Docker - ShareSafely

Guia para ejecutar el proyecto con Docker.

## Requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) 4.0+
- [Docker Compose](https://docs.docker.com/compose/) v2.0+

## Inicio Rapido

### 1. Configurar variables de entorno

```bash
cp .env.example .env
```

Editar `.env` con tus valores:

```env
DB_PASSWORD=TuPasswordSeguro123!
AZURE_STORAGE_CONNECTION=<tu-connection-string>
KEYVAULT_URL=https://tu-keyvault.vault.azure.net/
```

### 2. Construir y ejecutar

```bash
docker-compose up --build
```

### 3. Acceder a los servicios

| Servicio | URL |
|----------|-----|
| Frontend | http://localhost |
| API | http://localhost:5000 |
| API Swagger | http://localhost:5000/swagger |
| SQL Server | localhost:1433 |

## Comandos Utiles

### Iniciar servicios

```bash
# Primer inicio (construye imagenes)
docker-compose up --build

# Inicios posteriores
docker-compose up

# En background
docker-compose up -d
```

### Detener servicios

```bash
# Detener
docker-compose down

# Detener y eliminar volumenes
docker-compose down -v
```

### Ver logs

```bash
# Todos los servicios
docker-compose logs -f

# Servicio especifico
docker-compose logs -f api
docker-compose logs -f db
docker-compose logs -f web
```

### Reconstruir un servicio

```bash
docker-compose build api
docker-compose up -d api
```

### Ejecutar comandos en contenedor

```bash
# Bash en API
docker exec -it sharesafely-api sh

# SQL en base de datos
docker exec -it sharesafely-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -C
```

## Estructura de Contenedores

```
┌─────────────────────────────────────────────────────────┐
│                  docker-compose                          │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌────────────┐   ┌────────────┐   ┌────────────┐      │
│  │    web     │──▶│    api     │──▶│     db     │      │
│  │  (nginx)   │   │ (asp.net)  │   │ (mssql)    │      │
│  │   :80      │   │   :5000    │   │   :1433    │      │
│  └────────────┘   └────────────┘   └────────────┘      │
│                                                          │
│  Red: sharesafely-network                               │
│  Volumen: sqldata (persistencia)                        │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

## Desarrollo Local

### Hot Reload para API

Crear `docker-compose.override.yml`:

```yaml
version: '3.8'
services:
  api:
    volumes:
      - ./src/ShareSafely.API:/app
    environment:
      - DOTNET_USE_POLLING_FILE_WATCHER=1
```

### Conectar a SQL Server

Usar Azure Data Studio o SQL Server Management Studio:

- **Server:** localhost,1433
- **User:** sa
- **Password:** (valor de DB_PASSWORD en .env)
- **Database:** ShareSafelyDB

## Despliegue en Azure

### Azure Container Registry

```bash
# Login
az acr login --name turegistry

# Tag imagenes
docker tag sharesafely-api turegistry.azurecr.io/sharesafely-api:v1
docker tag sharesafely-web turegistry.azurecr.io/sharesafely-web:v1

# Push
docker push turegistry.azurecr.io/sharesafely-api:v1
docker push turegistry.azurecr.io/sharesafely-web:v1
```

### Opciones de hosting en Azure

| Servicio | Uso recomendado |
|----------|-----------------|
| **Azure Container Apps** | Microservicios, escalado automatico |
| **Azure App Service** | Web apps simples |
| **Azure Kubernetes (AKS)** | Orquestacion compleja |

### Cambios para Produccion

1. Usar Azure SQL Database en lugar de contenedor SQL
2. Configurar Azure Key Vault para secretos
3. Habilitar HTTPS con certificados
4. Configurar Application Insights
5. Usar Azure Blob Storage real

## Troubleshooting

### Error: Puerto en uso

```bash
# Ver que usa el puerto
lsof -i :5000
netstat -tulpn | grep 5000

# Cambiar puerto en docker-compose.yml
ports:
  - "5001:5000"
```

### Error: SQL Server no inicia

```bash
# Ver logs
docker-compose logs db

# Verificar password (minimo 8 caracteres, mayusculas, numeros)
# Reiniciar con volumen limpio
docker-compose down -v
docker-compose up --build
```

### Error: API no conecta a DB

```bash
# Verificar que db este healthy
docker-compose ps

# Probar conexion manual
docker exec -it sharesafely-api sh
# Dentro del contenedor:
ping db
```
