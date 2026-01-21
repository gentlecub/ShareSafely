#!/bin/bash
# ============================================================
# Crear App Service Plan y Web App
# ============================================================
source "$(dirname "$0")/00-variables.sh"

echo ""
echo "Creando App Service Plan: $APP_SERVICE_PLAN..."

# Crear App Service Plan
az appservice plan create \
    --name $APP_SERVICE_PLAN \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --sku B1 \
    --is-linux

if [ $? -ne 0 ]; then
    echo "Error al crear App Service Plan"
    exit 1
fi

echo "App Service Plan creado"

# Crear Web App
echo ""
echo "Creando Web App: $WEB_APP_NAME..."

az webapp create \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --plan $APP_SERVICE_PLAN \
    --runtime "DOTNETCORE:8.0"

if [ $? -ne 0 ]; then
    echo "Error al crear Web App"
    exit 1
fi

echo "Web App creada"

# Habilitar Managed Identity
echo ""
echo "Habilitando Managed Identity..."

az webapp identity assign \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP

# Configurar variables de entorno
echo ""
echo "Configurando variables de entorno..."

az webapp config appsettings set \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
        KeyVault__Url="https://${KEY_VAULT_NAME}.vault.azure.net/" \
        ASPNETCORE_ENVIRONMENT="Production"

# Dar acceso al Key Vault
echo ""
echo "Configurando acceso a Key Vault..."

WEBAPP_PRINCIPAL_ID=$(az webapp identity show \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --query principalId \
    --output tsv)

az keyvault set-policy \
    --name $KEY_VAULT_NAME \
    --object-id $WEBAPP_PRINCIPAL_ID \
    --secret-permissions get list

echo ""
echo "=============================================="
echo "Web App configurada"
echo "URL: https://${WEB_APP_NAME}.azurewebsites.net"
echo "=============================================="
