# Seguridad del Proyecto

- Nunca exponer claves de acceso en código
- Acceder a secretos solo mediante Azure Key Vault
- Enlaces generados con:
  - Token único
  - Fecha de expiración
  - Permisos solo de lectura
  - HTTPS obligatorio
- Validación de tamaño y tipo de archivos