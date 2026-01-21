#!/bin/bash
# ============================================================
# Ejecutar migración de base de datos
# ============================================================

SCRIPT_DIR="$(dirname "$0")"
source "$SCRIPT_DIR/../azure/00-variables.sh"

echo ""
echo "=============================================="
echo "ShareSafely - Migración de Base de Datos"
echo "=============================================="

# Verificar sqlcmd
if ! command -v sqlcmd &> /dev/null; then
    echo ""
    echo "sqlcmd no está instalado."
    echo ""
    echo "Opciones para ejecutar el script:"
    echo ""
    echo "1. Azure Portal:"
    echo "   - Ir a tu SQL Database"
    echo "   - Click en 'Query editor'"
    echo "   - Pegar contenido de 01-create-tables.sql"
    echo ""
    echo "2. Azure Data Studio:"
    echo "   - Conectar a ${SQL_SERVER_NAME}.database.windows.net"
    echo "   - Abrir 01-create-tables.sql"
    echo "   - Ejecutar"
    echo ""
    echo "3. Instalar sqlcmd:"
    echo "   - Ubuntu: sudo apt install mssql-tools"
    echo "   - Mac: brew install mssql-tools"
    echo ""
    exit 1
fi

echo ""
echo "Servidor: ${SQL_SERVER_NAME}.database.windows.net"
echo "Database: ${SQL_DATABASE_NAME}"
echo ""

read -p "¿Ejecutar migración? (s/n): " CONFIRM
if [[ $CONFIRM != "s" ]]; then
    echo "Cancelado"
    exit 0
fi

echo ""
echo "Ejecutando script SQL..."

sqlcmd \
    -S "${SQL_SERVER_NAME}.database.windows.net" \
    -d "$SQL_DATABASE_NAME" \
    -U "$SQL_ADMIN_USER" \
    -P "$SQL_ADMIN_PASSWORD" \
    -i "$SCRIPT_DIR/01-create-tables.sql"

if [ $? -eq 0 ]; then
    echo ""
    echo "=============================================="
    echo "Migración completada exitosamente"
    echo "=============================================="
else
    echo ""
    echo "Error en la migración"
    exit 1
fi
