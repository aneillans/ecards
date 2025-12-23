FROM node:22-alpine AS build

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build the app
RUN npm run build

# Production stage with hardened nginx
FROM dhi.io/nginx:1-alpine3.21

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy default blocked agents config template
COPY blocked-agents.conf /etc/nginx/blocked-agents.conf.default

# Copy entrypoint script
COPY docker-entrypoint.sh /docker-entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/bin/sh", "/docker-entrypoint.sh"]
