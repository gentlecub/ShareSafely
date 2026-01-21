# ============================================================
# ShareSafely Frontend - Dockerfile (Nginx)
# ============================================================

FROM nginx:alpine

# Eliminar configuración por defecto
RUN rm -rf /usr/share/nginx/html/*

# Copiar archivos estáticos
COPY src/ShareSafely.Web/ /usr/share/nginx/html/

# Copiar configuración personalizada de Nginx
COPY docker/web/nginx.conf /etc/nginx/conf.d/default.conf

# Usuario no-root (nginx ya lo maneja internamente)
# Permisos correctos
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chmod -R 755 /usr/share/nginx/html

# Puerto expuesto
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/ || exit 1

# Comando por defecto de nginx
CMD ["nginx", "-g", "daemon off;"]
