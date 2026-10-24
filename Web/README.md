# Package Manager Web

ASP.NET Core + SQLite API hosting a Blazor WebAssembly (MudBlazor) UI, in one container.

- `src/PackageManager.Shared` — DTOs, file-name/version parsing (netstandard2.1, reusable from Unity)
- `src/PackageManager.Server` — API, file storage, Verdaccio (UPM) proxy, Asset Store reader
- `src/PackageManager.Client` — Blazor WASM UI
- `tests/PackageManager.Tests` — xUnit

## Run locally

```bash
dotnet run --project src/PackageManager.Server --launch-profile http
```

Opens on http://localhost:5064. Data goes to `src/PackageManager.Server/App_Data/` (git-ignored).
The Development admin login is in `appsettings.Development.json` (never published).

## Deploy (NAS)

Pushing `Web/**` to `main` runs `.github/workflows/web-image.yml`: tests, then builds
`ghcr.io/thanhdv143/upkgs-web` (`latest` + commit SHA, amd64/arm64).

On the NAS, next to `compose.yaml`, create `.env`:

```
ADMIN_PASSWORD=...
PUID=1026   # example — use the UID/GID that owns the NAS folders (`id <user>`)
PGID=100
```

Without `PUID`/`PGID` the container runs as root.

then update manually with `docker compose pull && docker compose up -d`.

The old `unitypkgs/` folder is mounted as-is; its files show up under **File chưa quản lý**,
where they can be grouped into packages. Old direct links `/unitypkgs/<file>` keep working.

If a reverse proxy sits in front, raise its request-body limit for uploads (files can be hundreds of MB).

## Database changes

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/PackageManager.Server -s src/PackageManager.Server -o Data/Migrations
```

Migrations run automatically at startup.
