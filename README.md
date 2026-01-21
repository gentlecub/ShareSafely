# ShareSafely

Plataforma de gestión de archivos en Azure con enlaces temporales seguros.

## Descripcion

ShareSafely permite subir archivos a Azure Blob Storage y generar enlaces seguros con expiracion automatica. Los archivos se eliminan automaticamente cuando expiran.

## Caracteristicas

- Subida de archivos a Azure Blob Storage
- Generacion de enlaces SAS con expiracion configurable
- Validacion de tipo y tamanio de archivos
- Limpieza automatica de archivos expirados
- Secretos protegidos con Azure Key Vault
- Monitoreo con Application Insights

## Tecnologias

| Componente | Tecnologia |
|------------|------------|
| Backend | ASP.NET Core 8.0 |
| Frontend | HTML/CSS/JavaScript |
| Base de datos | Azure SQL Database |
| Almacenamiento | Azure Blob Storage |
| Secretos | Azure Key Vault |
| Limpieza | Azure Functions |
| Hosting | Azure App Service |

## Estructura del Proyecto

```
ShareSafely/
├── docs/                          # Documentacion de planificacion
├── scripts/
│   ├── azure/                     # Scripts de infraestructura
│   │   ├── 00-variables.sh
│   │   ├── 01-resource-group.sh
│   │   ├── 02-storage.sh
│   │   ├── 03-keyvault.sh
│   │   ├── 04-sql-database.sh
│   │   ├── 05-app-service.sh
│   │   ├── 06-function-app.sh
│   │   └── deploy-all.sh
│   └── database/                  # Scripts de migracion
│       ├── 01-create-tables.sql
│       ├── 02-seed-data.sql
│       └── run-migration.sh
├── src/
│   ├── ShareSafely.API/           # Backend ASP.NET Core
│   ├── ShareSafely.Web/           # Frontend
│   └── ShareSafely.Functions/     # Azure Function
└── README.md
```

## Requisitos Previos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- Suscripcion de Azure activa

## Instalacion Local

### 1. Clonar el repositorio

```bash
git clone https://github.com/gentlecub/ShareSafely.git
cd ShareSafely
```

### 2. Configurar appsettings.json

Editar `src/ShareSafely.API/appsettings.json`:

```json
{
  "AzureStorage": {
    "ConnectionString": "<TU-CONNECTION-STRING>",
    "ContainerName": "archivos"
  },
  "ConnectionStrings": {
    "DefaultConnection": "<TU-SQL-CONNECTION-STRING>"
  }
}
```

### 3. Ejecutar migracion de base de datos

```bash
# Opcion 1: Azure Portal Query Editor
# Copiar contenido de scripts/database/01-create-tables.sql

# Opcion 2: sqlcmd
cd scripts/database
./run-migration.sh
```

### 4. Ejecutar el backend

```bash
cd src/ShareSafely.API
dotnet run
```

La API estara disponible en: `https://localhost:7001`

### 5. Abrir el frontend

Abrir `src/ShareSafely.Web/index.html` en el navegador.

## Despliegue en Azure

### 1. Configurar variables

Editar `scripts/azure/00-variables.sh` con tus valores:

```bash
PROJECT_NAME="sharesafely"
LOCATION="eastus"
SQL_ADMIN_PASSWORD="TuPasswordSeguro123!"
```

### 2. Ejecutar despliegue completo

```bash
cd scripts/azure
./deploy-all.sh
```

Esto creara:
- Resource Group
- Storage Account + Container
- Key Vault + Secretos
- SQL Server + Database
- App Service

### 3. Desplegar la aplicacion

```bash
cd src/ShareSafely.API
dotnet publish -c Release -o ./publish
cd publish
zip -r ../publish.zip .
az webapp deploy --name sharesafely-api --src-path ../publish.zip
```

### 4. Desplegar Azure Function

```bash
cd src/ShareSafely.Functions
func azure functionapp publish sharesafely-cleanup
```

## API Endpoints

| Metodo | Endpoint | Descripcion |
|--------|----------|-------------|
| POST | `/api/files/upload` | Subir archivo |
| GET | `/api/files/{id}` | Obtener info del archivo |
| DELETE | `/api/files/{id}` | Eliminar archivo |
| POST | `/api/links/generate` | Generar enlace SAS |
| GET | `/api/links/download/{token}` | Descargar archivo |
| DELETE | `/api/links/{id}` | Revocar enlace |
| GET | `/health` | Health check |

## Ejemplo de Uso

### Subir archivo

```bash
curl -X POST https://localhost:7001/api/files/upload \
  -F "archivo=@documento.pdf" \
  -F "expiracionMinutos=60"
```

### Generar enlace

```bash
curl -X POST https://localhost:7001/api/links/generate \
  -H "Content-Type: application/json" \
  -d '{"archivoId": "guid-del-archivo", "expiracionMinutos": 60}'
```

## Configuracion

### Archivos permitidos

Editar en `appsettings.json`:

```json
"FileValidation": {
  "MaxFileSizeMB": 100,
  "AllowedExtensions": [".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg", ".zip"]
}
```

### Expiracion de enlaces

```json
"SasLink": {
  "DefaultExpirationMinutes": 60,
  "MaxExpirationMinutes": 1440
}
```

## Seguridad

- Todos los secretos en Azure Key Vault
- Enlaces SAS con permisos de solo lectura
- HTTPS obligatorio
- Validacion de tipo y tamanio de archivos
- Sin acceso publico al Blob Storage

## Licencia

MIT License
