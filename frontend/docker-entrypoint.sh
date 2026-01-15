#!/bin/sh
set -e

# Generate config.js from environment variables in /tmp (always writable)
cat > /tmp/config.js <<EOF
// Runtime configuration - injected by container startup
window.ENV = {
  KEYCLOAK_URL: '${KEYCLOAK_URL:-https://your-keycloak.example/}',
  KEYCLOAK_REALM: '${KEYCLOAK_REALM:-your-realm}',
  KEYCLOAK_CLIENT: '${KEYCLOAK_CLIENT:-ecards-frontend}',
  API_URL: '${API_URL:-/api}',
  SUPPORT_EMAIL: '${SUPPORT_EMAIL:-support@example.com}',
  SOCIAL_FACEBOOK: '${SOCIAL_FACEBOOK:-}',
  SOCIAL_TWITTER: '${SOCIAL_TWITTER:-}',
  SOCIAL_INSTAGRAM: '${SOCIAL_INSTAGRAM:-}',
  SOCIAL_DISCORD: '${SOCIAL_DISCORD:-}',
  SOCIAL_GITHUB: '${SOCIAL_GITHUB:-}',
  SOCIAL_MASTODON: '${SOCIAL_MASTODON:-}',
  SOCIAL_KOFI: '${SOCIAL_KOFI:-}'
};
EOF

echo "Generated runtime config at /tmp/config.js"
cat /tmp/config.js

# Copy default blocked agents config if not already present (not mounted)
if [ ! -f /etc/nginx/blocked-agents.conf ]; then
  echo "No custom blocked-agents.conf found, using default"
  cp /etc/nginx/blocked-agents.conf.default /etc/nginx/blocked-agents.conf
else
  echo "Using mounted blocked-agents.conf"
fi

# Start nginx
exec nginx -g "daemon off;"
