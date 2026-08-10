# Application Launcher

Zero-downtime launcher for Lens + Forge application stack running on Windows with Docker + WSL 2.

## Prerequisites

- Windows 10/11 Pro, Enterprise, or Education
- WSL 2 with Ubuntu
- Docker Engine installed in WSL 2
- .NET 8 Runtime (for running the launcher)

## Quick Start

### 1. Initial Setup

```powershell
# Clone this repository
git clone https://github.com/YOUR_ORG/launcher.git
cd launcher

# Copy and configure environment
Copy-Item .env.example .env
# Edit .env with your credentials
notepad .env
```

### 2. First Launch

```powershell
# Download and run the latest launcher
# Or compile from source:
dotnet build -c Release
.\bin\Release\net8.0-windows\AppLauncher.exe
```

### 3. Check for Updates

Click "Check Updates" button → If updates available, click "Update"

## Configuration

### Environment Variables

Copy `.env.example` to `.env` and configure:

| Variable | Description | Default |
|---|---|---|
| `POSTGRES_DB` | Database name | myapp_db |
| `POSTGRES_USER` | DB username | postgres |
| `POSTGRES_PASSWORD` | DB password | (required) |
| `SPRING_PROFILES_ACTIVE` | Spring profile | prod |
| `APP_ENV` | Application environment | production |

### Docker Images

Launcher pulls images from GitHub Container Registry (GHCR):

- Frontend: `ghcr.io/YOUR_ORG/lens:latest`
- Backend: `ghcr.io/YOUR_ORG/forge:latest`

Ensure both repositories are configured to publish to GHCR.

## Operations

### Update Application

1. Click "Check Updates"
2. Review available versions
3. Click "Update"
4. Launcher will:
   - Stop running containers
   - Backup database
   - Pull latest images
   - Start new containers
   - Verify health checks
   - Open browser to application

### Rollback

Click "Rollback" to revert to previous version instantly.

### Start Application

Click "Start App" to ensure containers are running and open browser.

## Logs

Application logs are stored in `launcher.log` in the launcher directory.

To view Docker container logs:

```powershell
wsl -e docker compose logs -f
```

## Troubleshooting

### Images pull fails

- Verify GHCR access: `wsl -e docker login ghcr.io`
- Check `.env` contains correct GHCR credentials
- Verify repository visibility (public or authenticated)

### Health checks fail

```powershell
# Check container status
wsl -e docker compose ps

# View logs
wsl -e docker compose logs forge
wsl -e docker compose logs lens
```

### Database issues

Database backups are stored as `db_backup_*.tar.gz`. To restore:

```powershell
wsl -e docker run --rm -v postgres_data:/data -v .:/backup alpine tar xzf /backup/db_backup_YYYYMMDD_HHMMSS.tar.gz -C /
```

## Development

### Build EXE Release

```powershell
dotnet publish -c Release -o ./publish
```

EXE will be in `publish/AppLauncher.exe`