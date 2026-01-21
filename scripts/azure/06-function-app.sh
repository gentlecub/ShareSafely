#!/bin/bash
# ============================================================
# Crear Function App para limpieza automática
# ============================================================
source "$(dirname "$0")/00-variables.sh"

FUNCTION_APP_NAME="${PROJECT_NAME}-cleanup"
FUNCTION_STORAGE="${PROJECT_NAME}funcstorage"

echo ""
echo "Creando Storage para Functions: $FUNCTION_STORAGE..."

# Storage Account para Functions (requerido)
az storage account create \
    --name $FUNCTION_STORAGE \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --sku Standard_LRS \
    --kind StorageV2

echo ""
echo "Creando Function App: $FUNCTION_APP_NAME..."

# Crear Function App
az functionapp create \
    --name $FUNCTION_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --storage-account $FUNCTION_STORAGE \
    --consumption-plan-location $LOCATION \
    --runtime dotnet-isolated \
    --runtime-version 8 \
    --functions-version 4 \
    --os-type Linux

if [ $? -ne 0 ]; then
    echo "Error al crear Function App"
    exit 1
fi

echo "Function App creada"

# Habilitar Managed Identity
echo ""
echo "Habilitando Managed Identity..."

az functionapp identity assign \
    --name $FUNCTION_APP_NAME \
    --resource-group $RESOURCE_GROUP

# Configurar variables de entorno
echo ""
echo "Configurando variables de entorno..."

STORAGE_CONN=$(az storage account show-connection-string \
    --name $STORAGE_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --query connectionString -o tsv)

SQL_CONN="Server=tcp:${SQL_SERVER_NAME}.database.windows.net,1433;Database=${SQL_DATABASE_NAME};User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};Encrypt=True;"

az functionapp config appsettings set \
    --name $FUNCTION_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
        "AzureStorage:ConnectionString=$STORAGE_CONN" \
        "AzureStorage:ContainerName=archivos" \
        "ConnectionStrings:DefaultConnection=$SQL_CONN"

# Dar acceso al Key Vault
echo ""
echo "Configurando acceso a Key Vault..."

FUNC_PRINCIPAL_ID=$(az functionapp identity show \
    --name $FUNCTION_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --query principalId -o tsv)

az keyvault set-policy \
    --name $KEY_VAULT_NAME \
    --object-id $FUNC_PRINCIPAL_ID \
    --secret-permissions get list

echo ""
echo "=============================================="
echo "Function App configurada"
echo "URL: https://${FUNCTION_APP_NAME}.azurewebsites.net"
echo "=============================================="
