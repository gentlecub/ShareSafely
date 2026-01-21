#!/bin/bash
# ============================================================
# Crear Key Vault y agregar secretos
# ============================================================
source "$(dirname "$0")/00-variables.sh"

echo ""
echo "Creando Key Vault: $KEY_VAULT_NAME..."

# Crear Key Vault
az keyvault create \
    --name $KEY_VAULT_NAME \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --enable-rbac-authorization false \
    --tags $TAGS

if [ $? -ne 0 ]; then
    echo "Error al crear Key Vault"
    exit 1
fi

echo "Key Vault creado exitosamente"

# Obtener connection string del Storage
echo ""
echo "Obteniendo Storage connection string..."

STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
    --name $STORAGE_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --query connectionString \
    --output tsv)

# Agregar secretos
echo ""
echo "Agregando secretos al Key Vault..."

# Secreto: Storage Connection String
az keyvault secret set \
    --vault-name $KEY_VAULT_NAME \
    --name "StorageConnectionString" \
    --value "$STORAGE_CONNECTION_STRING"

echo "- StorageConnectionString agregado"

# Secreto: SQL Connection String (placeholder)
SQL_CONNECTION_STRING="Server=tcp:${SQL_SERVER_NAME}.database.windows.net,1433;Database=${SQL_DATABASE_NAME};User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};Encrypt=True;"

az keyvault secret set \
    --vault-name $KEY_VAULT_NAME \
    --name "DatabaseConnectionString" \
    --value "$SQL_CONNECTION_STRING"

echo "- DatabaseConnectionString agregado"

# Secreto: Duración SAS Token
az keyvault secret set \
    --vault-name $KEY_VAULT_NAME \
    --name "SasTokenDuration" \
    --value "60"

echo "- SasTokenDuration agregado"

echo ""
echo "=============================================="
echo "Key Vault configurado exitosamente"
echo "URL: https://${KEY_VAULT_NAME}.vault.azure.net/"
echo "=============================================="
