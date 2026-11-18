# Deploying Kapson HRMS

One command rebuilds the app and publishes it to IIS on this server, with tests, a database
backup and automatic rollback.

```powershell
kapson\deploy\deploy.cmd
```

Windows asks once for Administrator rights (UAC). Every run is logged to `deploy\logs\`.

## Options

| Command | What it does |
|---|---|
| `deploy.cmd` | Unit tests → DB backup → publish → deploy to IIS → health check → smoke tests |
| `deploy.cmd -Pull` | `git pull --ff-only` first, then the above — the usual "update the server" command |
| `deploy.cmd -Apk` | Also build the signed employee APK into `android-release\` |
| `deploy.cmd -ApkOnly` | Only the APK |
| `deploy.cmd -DryRun -Apk` | Tests, publish, smoke tests and APK without touching IIS or the database (no admin needed) |
| `-SkipTests`, `-SkipBackup` | Faster, for emergencies only |
| `-SiteUrl https://…` | Public address for smoke tests, when the tunnel address changes |

## What it protects

* **Server-only files are never overwritten**: `appsettings.Production.json` (signing key, HTTPS
  port), `web.config`, `.data\` (uploads), `.data-protection-keys\` (encryption keys) and `logs\`.
* **Database backup before every deploy** (`COPY_ONLY`, in the SQL Server Backup folder). The app
  applies its EF Core migrations when it starts, so the backup is the undo button for those.
* **Rollback**: the previous build is copied to `.build\rollback` first. If `/kapson_home` doesn't
  answer 200 after the deploy, the previous build is put back automatically.
* Visitors see a short "updating" page (`app_offline.htm`) during the copy instead of errors.

## APK versions

Each APK gets version code `yyMMddHH` (build time), so a newer APK always installs over an older
one. It's signed with `android-signing\kapsons-employee.keystore` — back that folder up; without it
updates can't be installed over the existing app.

## One-time setup on a new server

* .NET 9 SDK, IIS with the ASP.NET Core Hosting Bundle and WebSocket Protocol, SQL Server.
* For the APK: `dotnet workload install maui-android` (admin), then let the first APK build
  install the Android SDK pieces and a JDK into `%LOCALAPPDATA%\Android`.
