#!/bin/bash
# ============================================================
# Crear SQL Server y Database
# ============================================================
source "$(dirname "$0")/00-variables.sh"

echo ""
echo "Creando SQL Server: $SQL_SERVER_NAME..."

# Crear SQL Server
az sql server create \
    --name $SQL_SERVER_NAME \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --admin-user $SQL_ADMIN_USER \
    --admin-password $SQL_ADMIN_PASSWORD

if [ $? -ne 0 ]; then
    echo "Error al crear SQL Server"
    exit 1
fi

echo "SQL Server creado exitosamente"

# Configurar firewall - Permitir servicios de Azure
echo ""
echo "Configurando firewall..."

az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "AllowAzureServices" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0

# Obtener IP pública actual para desarrollo
MY_IP=$(curl -s ifconfig.me)

az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "AllowMyIP" \
    --start-ip-address $MY_IP \
    --end-ip-address $MY_IP

echo "Firewall configurado"

# Crear Database
echo ""
echo "Creando Database: $SQL_DATABASE_NAME..."

az sql db create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name $SQL_DATABASE_NAME \
    --service-objective Basic \
    --backup-storage-redundancy Local \
    --tags $TAGS

if [ $? -eq 0 ]; then
    echo "Database creada exitosamente"
else
    echo "Error al crear Database"
    exit 1
fi

echo ""
echo "=============================================="
echo "SQL Database configurada"
echo "Server: ${SQL_SERVER_NAME}.database.windows.net"
echo "Database: $SQL_DATABASE_NAME"
echo "=============================================="
