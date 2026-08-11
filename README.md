# Clinix Launcher

Professional Windows launcher for the Clinix stack (Lens + Forge + Postgres) running on **WSL 2 + Docker Engine**.

## Features

- Professional light UI with step progress for long downloads and live activity log
- First-run setup for WSL and Docker Engine inside WSL
- Start / Stop / Open App
- Manual and automatic update checks (prompt before apply)
- Rollback to previous image set
- One-click crash reports via GitHub Issues API
- Ships `docker-compose.yml` and `.env.example` beside the EXE on build/publish

## Prerequisites

- Windows 10/11
- .NET 8 runtime (or use the self-contained release EXE)
- Administrator rights for first-time WSL install (if needed)

Docker Desktop is **not** required. The launcher installs/uses Docker Engine inside WSL when possible.

## Quick start

1. Download the latest release assets (`AppLauncher-*.exe`, `docker-compose.yml`, and `.env.example`) into one folder — or build from source (both files are copied next to the EXE).
2. Copy `.env.example` → `.env` (if missing) and set at least `POSTGRES_PASSWORD`.
3. Run `AppLauncher.exe`.
4. If setup is needed, click **Retry Setup** and approve elevation / Ubuntu prompts.
5. Click **Start**, then **Open App** (`http://localhost`).

## Configuration

| Variable | Description | Default |
|---|---|---|
| `POSTGRES_DB` | Database name | `clinix_datastore` |
| `POSTGRES_USER` | DB username | `clinix_application` |
| `POSTGRES_PASSWORD` | DB password | (required) |
| `GHCR_ORG` | GHCR organization | `clinix-rk` |
| `GHCR_TOKEN` | Optional token for private GHCR pulls / tag listing | |
| `GITHUB_REPORT_REPO` | `owner/repo` for crash issues | `clinix-rk/launcher` |
| `GITHUB_REPORT_TOKEN` | Fine-grained PAT with Issues write | |
| `GITHUB_REPORT_LABELS` | Comma-separated labels | `crash-report` |
| `AUTO_UPDATE_ENABLED` | Check for updates on a timer | `true` |
| `AUTO_UPDATE_INTERVAL_HOURS` | Hours between auto checks | `6` |

### Crash report PAT (maintainers)

1. GitHub → Settings → Developer settings → Fine-grained personal access tokens.
2. Resource owner: your org/user; repository access: only `clinix-rk/launcher` (or your fork).
3. Permissions: **Issues** → Read and write.
4. Put the token in `.env` as `GITHUB_REPORT_TOKEN`.
5. Ensure label `crash-report` exists on the repo (or change `GITHUB_REPORT_LABELS`).

## Operations

### Start / Stop

- **Start** runs `docker compose up -d`, waits for health, and streams compose logs.
- **Stop** runs `docker compose down` (volumes preserved).
- **Open App** opens `http://localhost` when Lens is healthy.

### Updates

1. **Check Updates** queries GHCR for Lens/Forge tags.
2. **Update** stops services, backs up the DB volume, `docker compose pull`, starts again, verifies health, and records the version.
3. Failed health after update triggers **rollback**.
4. With **Auto-check updates** enabled, the launcher checks on launch and every N hours, then prompts before applying.

### Crash reports

**Send Crash Report** creates a GitHub issue containing:

- Launcher version and machine metadata
- Docker / WSL versions
- `docker compose ps`
- Recent container logs
- Tail of `launcher.log`

## Logs

- On-screen **Activity log** (Clear / Copy)
- File: `launcher.log` next to the EXE
- DB backups: `db_backup_*.tar.gz` next to the EXE

## Build from source

Place `docker-compose.yml` at the **repo root** (next to `.env.example`) before building. The project copies both into the output directory the same way.

```powershell
dotnet publish -c Release -o ./publish `
  --self-contained `
  -p:PublishSingleFile=true `
  -p:RuntimeIdentifier=win-x64 `
  src/AppLauncher/AppLauncher.csproj
```

Tagged pushes (`v*`) build and publish releases via `.github/workflows/build-release.yml`.

## Troubleshooting

### Setup stuck on Docker

```powershell
wsl --shutdown
wsl -e bash -lc "sudo service docker start && docker info"
```

If Docker was just installed, log out of the WSL distro (or `wsl --shutdown`) so `docker` group membership applies.

### Health checks fail

```powershell
wsl -e bash -lc "cd /mnt/c/path/to/launcher && docker compose ps && docker compose logs --tail 100"
```

Forge health: `http://localhost:8080/api/v1/actuator/health`  
Lens health: `http://localhost/`

### Images pull fails

```powershell
wsl -e bash -lc "echo $env:GHCR_TOKEN | docker login ghcr.io -u YOUR_USER --password-stdin"
```

(Or set `GHCR_USERNAME` / `GHCR_TOKEN` in `.env` for tag checks; compose pull still needs docker login for private images.)
