# Flujo de Subida de Archivos

1. Usuario sube archivo desde la web
2. Backend valida archivo
3. Backend guarda archivo en Azure Blob Storage
4. Se genera un SAS Token con tiempo limitado
5. Se crea un enlace único para compartir
6. Se guarda metadata del archivo:
   - Nombre
   - Fecha de subida
   - Expiración
   - Estado
7. Se muestra el enlace al usuario

# Generación de Enlaces
- Usar Shared Access Signatures (SAS)
- Enlace debe:
  - Expirar automáticamente
  - Permitir solo lectura
  - Asociarse a un solo archivo