#!/bin/sh
set -e

# Generate config.js from environment variables
cat > /usr/share/nginx/html/config.js <<EOF
// Runtime configuration - injected by container startup
window.ENV = {
  KEYCLOAK_URL: '${KEYCLOAK_URL:-https://your-keycloak.example/}',
  KEYCLOAK_REALM: '${KEYCLOAK_REALM:-your-realm}',
  KEYCLOAK_CLIENT: '${KEYCLOAK_CLIENT:-ecards-frontend}',
  API_URL: '${API_URL:-/api}'
};
EOF

echo "Generated runtime config:"
cat /usr/share/nginx/html/config.js

# Start nginx
exec nginx -g "daemon off;"
