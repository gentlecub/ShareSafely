# ShareSafely

File management platform on Azure with secure temporary links.

## Description

ShareSafely allows you to upload files to Azure Blob Storage and generate secure links with automatic expiration. Files are automatically deleted when they expire.

## Features

- File upload to Azure Blob Storage
- SAS link generation with configurable expiration
- File type and size validation
- Automatic cleanup of expired files
- Secrets protected with Azure Key Vault
- Monitoring with Application Insights

## Technologies

| Component | Technology |
|-----------|------------|
| Backend | ASP.NET Core 8.0 |
| Frontend | HTML/CSS/JavaScript |
| Database | Azure SQL Database |
| Storage | Azure Blob Storage |
| Secrets | Azure Key Vault |
| Cleanup | Azure Functions |
| Hosting | Azure App Service |

## Project Structure

```
ShareSafely/
├── docs/                          # Planning documentation
├── scripts/
│   ├── azure/                     # Infrastructure scripts
│   │   ├── 00-variables.sh
│   │   ├── 01-resource-group.sh
│   │   ├── 02-storage.sh
│   │   ├── 03-keyvault.sh
│   │   ├── 04-sql-database.sh
│   │   ├── 05-app-service.sh
│   │   ├── 06-function-app.sh
│   │   └── deploy-all.sh
│   └── database/                  # Migration scripts
│       ├── 01-create-tables.sql
│       ├── 02-seed-data.sql
│       └── run-migration.sh
├── src/
│   ├── ShareSafely.API/           # ASP.NET Core Backend
│   ├── ShareSafely.Web/           # Frontend
│   └── ShareSafely.Functions/     # Azure Function
└── README.md
```

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- Active Azure subscription

## Local Installation

### 1. Clone the repository

```bash
git clone https://github.com/gentlecub/ShareSafely.git
cd ShareSafely
```

### 2. Configure appsettings.json

Edit `src/ShareSafely.API/appsettings.json`:

```json
{
  "AzureStorage": {
    "ConnectionString": "<YOUR-CONNECTION-STRING>",
    "ContainerName": "archivos"
  },
  "ConnectionStrings": {
    "DefaultConnection": "<YOUR-SQL-CONNECTION-STRING>"
  }
}
```

### 3. Run database migration

```bash
# Option 1: Azure Portal Query Editor
# Copy contents from scripts/database/01-create-tables.sql

# Option 2: sqlcmd
cd scripts/database
./run-migration.sh
```

### 4. Run the backend

```bash
cd src/ShareSafely.API
dotnet run
```

The API will be available at: `https://localhost:7001`

### 5. Open the frontend

Open `src/ShareSafely.Web/index.html` in your browser.

## Azure Deployment

### 1. Configure variables

Edit `scripts/azure/00-variables.sh` with your values:

```bash
PROJECT_NAME="sharesafely"
LOCATION="eastus"
SQL_ADMIN_PASSWORD="YourSecurePassword123!"
```

### 2. Run complete deployment

```bash
cd scripts/azure
./deploy-all.sh
```

This will create:
- Resource Group
- Storage Account + Container
- Key Vault + Secrets
- SQL Server + Database
- App Service

### 3. Deploy the application

```bash
cd src/ShareSafely.API
dotnet publish -c Release -o ./publish
cd publish
zip -r ../publish.zip .
az webapp deploy --name sharesafely-api --src-path ../publish.zip
```

### 4. Deploy Azure Function

```bash
cd src/ShareSafely.Functions
func azure functionapp publish sharesafely-cleanup
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/files/upload` | Upload file |
| GET | `/api/files/{id}` | Get file info |
| DELETE | `/api/files/{id}` | Delete file |
| POST | `/api/links/generate` | Generate SAS link |
| GET | `/api/links/download/{token}` | Download file |
| DELETE | `/api/links/{id}` | Revoke link |
| GET | `/health` | Health check |

## Usage Example

### Upload file

```bash
curl -X POST https://localhost:7001/api/files/upload \
  -F "archivo=@document.pdf" \
  -F "expiracionMinutos=60"
```

### Generate link

```bash
curl -X POST https://localhost:7001/api/links/generate \
  -H "Content-Type: application/json" \
  -d '{"archivoId": "file-guid", "expiracionMinutos": 60}'
```

## Configuration

### Allowed files

Edit in `appsettings.json`:

```json
"FileValidation": {
  "MaxFileSizeMB": 100,
  "AllowedExtensions": [".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg", ".zip"]
}
```

### Link expiration

```json
"SasLink": {
  "DefaultExpirationMinutes": 60,
  "MaxExpirationMinutes": 1440
}
```

## Security

- All secrets stored in Azure Key Vault
- SAS links with read-only permissions
- HTTPS required
- File type and size validation
- No public access to Blob Storage

## License

MIT License
