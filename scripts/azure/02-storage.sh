#!/bin/bash
# ============================================================
# Crear Storage Account y Container
# ============================================================
source "$(dirname "$0")/00-variables.sh"

echo ""
echo "Creando Storage Account: $STORAGE_ACCOUNT..."

# Crear Storage Account
az storage account create \
    --name $STORAGE_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --sku Standard_LRS \
    --kind StorageV2 \
    --access-tier Hot \
    --https-only true \
    --allow-blob-public-access false \
    --tags $TAGS

if [ $? -ne 0 ]; then
    echo "Error al crear Storage Account"
    exit 1
fi

echo "Storage Account creado exitosamente"

# Obtener connection string
echo ""
echo "Obteniendo connection string..."

STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
    --name $STORAGE_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --query connectionString \
    --output tsv)

# Crear container
echo ""
echo "Creando container: $STORAGE_CONTAINER..."

az storage container create \
    --name $STORAGE_CONTAINER \
    --account-name $STORAGE_ACCOUNT \
    --public-access off

if [ $? -eq 0 ]; then
    echo "Container creado exitosamente"
else
    echo "Error al crear container"
    exit 1
fi

echo ""
echo "=============================================="
echo "STORAGE CONNECTION STRING (guardar en Key Vault):"
echo "=============================================="
echo "$STORAGE_CONNECTION_STRING"
echo "=============================================="
