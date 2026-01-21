#!/bin/bash
# ============================================================
# VARIABLES DE CONFIGURACIÓN - ShareSafely
# ============================================================
# Modifica estos valores según tu suscripción de Azure
# ============================================================

# Nombre base del proyecto (se usa como prefijo)
PROJECT_NAME="sharesafely"

# Ubicación de Azure (ver: az account list-locations)
LOCATION="eastus"

# Nombre del Resource Group
RESOURCE_GROUP="${PROJECT_NAME}-rg"

# Storage Account (debe ser único globalmente, solo minúsculas y números)
STORAGE_ACCOUNT="${PROJECT_NAME}storage"
STORAGE_CONTAINER="archivos"

# Key Vault (debe ser único globalmente)
KEY_VAULT_NAME="${PROJECT_NAME}-kv"

# SQL Server y Database
SQL_SERVER_NAME="${PROJECT_NAME}-sql"
SQL_DATABASE_NAME="${PROJECT_NAME}-db"
SQL_ADMIN_USER="sqladmin"
SQL_ADMIN_PASSWORD="P@ssw0rd123!"  # CAMBIAR EN PRODUCCIÓN

# App Service
APP_SERVICE_PLAN="${PROJECT_NAME}-plan"
WEB_APP_NAME="${PROJECT_NAME}-api"

# Tags para recursos
TAGS="project=sharesafely environment=dev"

# ============================================================
# NO MODIFICAR - Variables calculadas
# ============================================================
SUBSCRIPTION_ID=$(az account show --query id -o tsv 2>/dev/null)

echo "=============================================="
echo "Configuración de ShareSafely"
echo "=============================================="
echo "Proyecto:        $PROJECT_NAME"
echo "Ubicación:       $LOCATION"
echo "Resource Group:  $RESOURCE_GROUP"
echo "Storage:         $STORAGE_ACCOUNT"
echo "Key Vault:       $KEY_VAULT_NAME"
echo "SQL Server:      $SQL_SERVER_NAME"
echo "Web App:         $WEB_APP_NAME"
echo "=============================================="
