#!/bin/bash

# Railway assigns PORT dynamically, default to 8080
PORT=${PORT:-8080}

echo "Starting ShareSafely on port $PORT"

# Update nginx to listen on the correct port
sed -i "s/listen 8080/listen $PORT/g" /etc/nginx/sites-enabled/default

# Create log directory for supervisor
mkdir -p /var/log/supervisor

# Start supervisor (which starts nginx + API)
exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf
