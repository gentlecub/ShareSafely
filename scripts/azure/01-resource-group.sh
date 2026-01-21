#!/bin/bash
# ============================================================
# Crear Resource Group
# ============================================================
source "$(dirname "$0")/00-variables.sh"

echo ""
echo "Creando Resource Group: $RESOURCE_GROUP..."

az group create \
    --name $RESOURCE_GROUP \
    --location $LOCATION \
    --tags $TAGS

if [ $? -eq 0 ]; then
    echo "Resource Group creado exitosamente"
else
    echo "Error al crear Resource Group"
    exit 1
fi
