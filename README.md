# ecards

A self-hosted eCard application with a .NET 10 backend API and React/Vite frontend.

## Docker Images

Docker images for this project can be found on my public repo: 

https://packages.neillans.co.uk/containers/registry?feedId=2

This Proget server is available through the generosity of Inedo providing a license for Open Source projects.

## Quick Start

```bash
docker-compose up -d
```

This starts three services:
- **MariaDB** database on port 3306
- **API** at http://localhost:5000 (reverse-proxied from localhost:8080)
- **Frontend** at http://localhost:80

## Configuration

Both containers are configured via environment variables. Defaults work for local development; override in `docker-compose.yml` for production.

### API Configuration (ECards.Api)

#### Logging
- `Logging:LogLevel:Default` - Default log level (`Information`, `Warning`, etc.)
- `ASPNETCORE_ENVIRONMENT` - Environment name (`Development`, `Production`)

#### Database (MariaDB)
- `ConnectionStrings:DefaultConnection` - Full connection string. Default: `Server=mariadb;Port=3306;Database=ecards;User=ecards;Password=ecards123;`

#### Storage & Upload Limits
- `Storage:CustomArtPath` - Path where user-uploaded artwork is stored. Default: `/app/storage/custom`
- `Storage:PremadeArtPath` - Path for premade template images. Default: `/app/storage/premade`
- `Storage:MaxUploadBytes` - Maximum file size (bytes) for custom art uploads. Default: `5242880` (5 MB)
- `Storage:MaxImageDimension` - Maximum pixel dimension (width/height); images larger than this are resized preserving aspect ratio. Default: `1600`

#### URLs & Reverse Proxy
- `PathBase` - Path prefix for reverse proxy scenarios (e.g., `/api`). Used to construct correct URLs and redirect paths.
- `BaseUrl` - Public base URL of the API. Default: `http://localhost:5000`
- `FrontendUrl` - Public URL of the frontend for email links. Default: `http://localhost:80`

#### Authentication (OpenID Connect / Keycloak)
- `Authentication:Authority` - OIDC authority/realm URL. Example: `https://auth.example.com/realms/eCards`
- `Authentication:Audience` - JWT audience claim. Default: `account`
- `Authentication:RequireHttpsMetadata` - Require HTTPS metadata from authority. Default: `false` (disable for dev)

#### CORS
- `Cors:AllowedOrigins:0`, `Cors:AllowedOrigins:1`, etc. - Array of allowed cross-origin request sources.
  - Default: `http://localhost:5173`, `http://localhost:3000`, `http://localhost:80`, `http://localhost`

#### Application
- `AppName` - Display name shown in UI and emails. Default: `eCards`

#### Email (SMTP)
- `Smtp:Host` - SMTP server hostname. Example: `smtp.gmail.com`
- `Smtp:Port` - SMTP port. Default: `465`
- `Smtp:EnableSsl` - Enable TLS/SSL. Default: `true`
- `Smtp:Username` - SMTP authentication username
- `Smtp:Password` - SMTP authentication password
- `Smtp:FromEmail` - Sender email address (from the card service)
- `Smtp:FromName` - Sender display name shown in emails

**Example environment override** (in `docker-compose.yml`):
```yaml
environment:
  - Smtp__Host=smtp.gmail.com
  - Smtp__Port=587
  - Smtp__Username=your-email@gmail.com
  - Smtp__Password=your-app-password
  - Smtp__FromEmail=noreply@your-domain.com
  - Smtp__FromName=eCards Service
```

### Frontend Configuration (React/Vite + Nginx)

#### API & Keycloak
- `API_URL` - Base URL for backend API calls. Default: `/api` (relative, proxied by nginx)
- `KEYCLOAK_URL` - Base URL of your Keycloak instance. Example: `https://auth.example.com/`
- `KEYCLOAK_REALM` - Keycloak realm name. Default: `eCards`
- `KEYCLOAK_CLIENT` - Keycloak client ID. Default: `eCards`

#### Optional Settings
- `SUPPORT_EMAIL` - Support contact email (not yet used but available for future features)

#### Social Links (optional)
Leave empty to hide these links from the footer:
- `SOCIAL_FACEBOOK` - Facebook page URL
- `SOCIAL_TWITTER` - Twitter/X profile URL
- `SOCIAL_INSTAGRAM` - Instagram profile URL
- `SOCIAL_DISCORD` - Discord server invite
- `SOCIAL_GITHUB` - GitHub repository URL
- `SOCIAL_MASTODON` - Mastodon profile URL
- `SOCIAL_KOFI` - Ko-fi support page URL

### MariaDB Configuration

- `MYSQL_ROOT_PASSWORD` - Root user password. Default: `rootpassword123`
- `MYSQL_DATABASE` - Database name. Default: `ecards`
- `MYSQL_USER` - Non-root user. Default: `ecards`
- `MYSQL_PASSWORD` - Non-root user password. Default: `ecards123`

Database runs on port 3306. Data is persisted in the `mariadb_data` volume.

## Volumes

- `mariadb_data` - MariaDB persistent storage
- `ecards_storage` - Custom art uploads and premade templates; shared between restarts

## Production Deployment

For production, override these at minimum:

**API:**
```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - Authentication__RequireHttpsMetadata=true
  - Cors__AllowedOrigins__0=https://your-domain.com
  # Update connection string with production database credentials
  - ConnectionStrings__DefaultConnection=Server=prod-db;Port=3306;Database=ecards;User=prod_user;Password=STRONG_PASSWORD;
  - Smtp__Host=your-smtp-server
  - Smtp__Username=smtp_user
  - Smtp__Password=STRONG_PASSWORD
```

**Frontend:**
```yaml
environment:
  - API_URL=https://your-domain.com/api
  - KEYCLOAK_URL=https://auth.your-domain.com/
  - KEYCLOAK_REALM=your-realm
  - KEYCLOAK_CLIENT=eCards
```

**MariaDB:**
- Use a managed database service or hardened MariaDB container with strong credentials
- Do not use default passwords

## Development

### Local Setup (without Docker)

**API (.NET 10):**
```bash
cd ECards.Api
dotnet run
```
Runs on `https://localhost:7123` (HTTPS) or `http://localhost:5000` (HTTP override).

**Frontend (Node 22+):**
```bash
cd frontend
npm install
npm run dev
```
Runs on `http://localhost:5173`.

### Database Migrations

Migrations are applied automatically on API startup. To create new migrations:
```bash
cd ECards.Api
dotnet ef migrations add MigrationName
```
