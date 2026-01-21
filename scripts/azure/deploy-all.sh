#!/bin/bash
# ============================================================
# SCRIPT MAESTRO - Desplegar toda la infraestructura
# ============================================================
# Ejecutar: ./deploy-all.sh
# ============================================================

set -e  # Detener si hay errores

SCRIPT_DIR="$(dirname "$0")"

echo "=============================================="
echo "  ShareSafely - Despliegue de Infraestructura"
echo "=============================================="
echo ""

# Verificar que Azure CLI está instalado
if ! command -v az &> /dev/null; then
    echo "Error: Azure CLI no está instalado"
    echo "Instalar: https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi

# Verificar login
echo "Verificando sesión de Azure..."
az account show &> /dev/null || {
    echo "No hay sesión activa. Iniciando login..."
    az login
}

echo ""
echo "Cuenta activa:"
az account show --query "{Nombre:name, ID:id}" -o table

echo ""
read -p "¿Continuar con esta cuenta? (s/n): " CONFIRM
if [[ $CONFIRM != "s" ]]; then
    echo "Cancelado"
    exit 0
fi

# Ejecutar scripts en orden
echo ""
echo "=============================================="
echo "Paso 1/5: Creando Resource Group..."
echo "=============================================="
bash "$SCRIPT_DIR/01-resource-group.sh"

echo ""
echo "=============================================="
echo "Paso 2/5: Creando Storage Account..."
echo "=============================================="
bash "$SCRIPT_DIR/02-storage.sh"

echo ""
echo "=============================================="
echo "Paso 3/5: Creando Key Vault..."
echo "=============================================="
bash "$SCRIPT_DIR/03-keyvault.sh"

echo ""
echo "=============================================="
echo "Paso 4/5: Creando SQL Database..."
echo "=============================================="
bash "$SCRIPT_DIR/04-sql-database.sh"

echo ""
echo "=============================================="
echo "Paso 5/5: Creando App Service..."
echo "=============================================="
bash "$SCRIPT_DIR/05-app-service.sh"

echo ""
echo "=============================================="
echo "  DESPLIEGUE COMPLETADO"
echo "=============================================="
source "$SCRIPT_DIR/00-variables.sh"
echo ""
echo "Recursos creados:"
echo "- Resource Group:  $RESOURCE_GROUP"
echo "- Storage:         $STORAGE_ACCOUNT"
echo "- Key Vault:       https://${KEY_VAULT_NAME}.vault.azure.net/"
echo "- SQL Server:      ${SQL_SERVER_NAME}.database.windows.net"
echo "- Web App:         https://${WEB_APP_NAME}.azurewebsites.net"
echo ""
echo "Siguiente paso: Desplegar la aplicación con:"
echo "  az webapp deploy --name $WEB_APP_NAME --src-path ./publish.zip"
echo "=============================================="
