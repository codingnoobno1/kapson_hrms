<#
.SYNOPSIS
    One-command rebuild and deploy of Kapson HRMS to IIS on this server, plus the employee APK.

.DESCRIPTION
    1. (optional) git pull           — -Pull
    2. unit tests                    — skip with -SkipTests
    3. database backup (COPY_ONLY)   — skip with -SkipBackup; the app migrates the DB on start
    4. dotnet publish (Release)
    5. app_offline → copy → online   — server-only files are never overwritten:
                                       appsettings.Production.json, web.config, .data, keys, logs
    6. health check; automatic rollback to the previous build if it fails
    7. smoke tests against the public URL
    8. (optional) signed release APK — -Apk

    Re-launches itself as Administrator (one UAC prompt). Everything is logged to
    deploy\logs\deploy-<timestamp>.log.

.EXAMPLE
    .\deploy.ps1                 # build, test, back up, deploy, check
    .\deploy.ps1 -Pull -Apk      # git pull first, and also build the APK
    .\deploy.ps1 -ApkOnly        # only build the APK
#>
[CmdletBinding()]
param(
    [switch]$Pull,
    [switch]$Apk,
    [switch]$ApkOnly,
    [switch]$SkipTests,
    [switch]$SkipBackup,
    [switch]$DryRun,       # tests + publish + smoke + APK only; touches neither IIS nor the database
    [switch]$NoPause,      # don't wait for Enter at the end (for scheduled or scripted runs)
    [string]$SiteUrl  = '',   # empty = the current HRMS address published by the Vertex link manager
    [string]$SitePath = 'C:\inetpub\HRMS',
    [string]$LocalUrl = 'http://127.0.0.1:8080',
    [string]$Database = 'VisplHRManagement'
)

$ErrorActionPreference = 'Stop'
$Kapson   = Split-Path -Parent $PSScriptRoot                      # ...\kapson
$RepoRoot = Split-Path -Parent $Kapson                             # ...\kapson_hrms
$Build    = Join-Path $Kapson '.build'
$Stamp    = Get-Date -Format 'yyyyMMdd-HHmmss'

# Public address for smoke tests: the link manager keeps the live tunnel address in links.json.
if (-not $SiteUrl) {
    try {
        $links = Get-Content 'C:\inetpub\wwwroot\links.json' -Raw -ErrorAction Stop | ConvertFrom-Json
        $SiteUrl = ($links.projects | Where-Object key -eq 'hrms' | Select-Object -First 1).tunnelUrl
    } catch { }
    if (-not $SiteUrl) { $SiteUrl = 'https://maritime-recognized-teachers-browser.trycloudflare.com' }
}

# ---- run as Administrator (IIS folder and SQL backup need it) -----------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin -and -not $DryRun) {
    $forward = $PSBoundParameters.GetEnumerator() | ForEach-Object {
        if ($_.Value -is [switch]) { if ($_.Value) { "-$($_.Key)" } } else { "-$($_.Key)", "`"$($_.Value)`"" }
    }
    Write-Host 'Requesting Administrator rights (UAC prompt)...'
    $p = Start-Process powershell -Verb RunAs -Wait -PassThru -ArgumentList (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"") + $forward)
    exit $p.ExitCode
}

New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'logs'), $Build | Out-Null
$Log = Join-Path $PSScriptRoot "logs\deploy-$Stamp.log"
Start-Transcript -Path $Log | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
function Step([string]$text) { Write-Host ''; Write-Host "==> $text  [$([int]$sw.Elapsed.TotalSeconds)s]" -ForegroundColor Cyan }
function Run([string]$exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$exe failed (exit $LASTEXITCODE)" }
}
function Test-Site {
    # The site sits behind Cloudflare; locally we say the request arrived over HTTPS, as cloudflared does.
    try {
        $r = Invoke-WebRequest "$LocalUrl/kapson_home" -Headers @{ 'X-Forwarded-Proto' = 'https' } -UseBasicParsing -TimeoutSec 90
        return $r.StatusCode -eq 200
    } catch { Write-Host "   health check: $($_.Exception.Message)"; return $false }
}

$exitCode = 0
try {
    Push-Location $Kapson     # global.json here pins the .NET 9 SDK

    if ($isAdmin) {
        # Builds run as Administrator leave obj\ and bin\ files that a normal (non-elevated)
        # session can't overwrite, which breaks dry runs and everyday builds. Give this
        # Windows account modify rights on the repository folder (only this folder).
        $null = icacls $RepoRoot /grant "$($env:USERDOMAIN)\$($env:USERNAME):(OI)(CI)M" /T /C /Q
    }

    if ($Pull) {
        Step 'git pull'
        Run git @('-C', $RepoRoot, 'pull', '--ff-only')
    }

    if (-not $ApkOnly) {
        if (-not $SkipTests) {
            Step 'Unit tests'
            Run dotnet @('test', 'Vispl.HRManagement.Tests\Vispl.HRManagement.Tests.csproj', '-c', 'Release', '--nologo', '-v', 'q', '--filter', 'Category!=Smoke')
        }

        if (-not $SkipBackup -and -not $DryRun) {
            Step "Backing up database $Database"
            $bak = "C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\$Database-predeploy-$Stamp.bak"
            Run sqlcmd @('-S', 'localhost', '-E', '-C', '-b', '-Q', "BACKUP DATABASE [$Database] TO DISK = N'$bak' WITH COPY_ONLY, INIT, CHECKSUM")
            Write-Host "   $bak"
        }

        if (-not $DryRun) {
            # The site's own SQL login may not create tables (least privilege), so schema
            # upgrades are applied here, as this administrator, before the new build goes live.
            # When the app then starts, its own migration check finds nothing left to do.
            Step 'Applying database migrations'
            Run dotnet @('tool', 'restore')
            $conn = "Server=localhost;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;"
            $env:ASPNETCORE_ENVIRONMENT = 'Development'   # design-time only; nothing is started
            try {
                Run dotnet @('ef', 'database', 'update', '--context', 'HrManagementDbContext', '--project', 'Vispl.HRManagement.DL',
                    '--startup-project', 'Vispl.HRManagement.Host', '--connection', $conn)
                Run dotnet @('ef', 'database', 'update', '--context', 'AdminIdentityDbContext', '--project', 'Vispl.HRManagement.Identity',
                    '--startup-project', 'Vispl.HRManagement.Host', '--connection', $conn)
            } finally { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue }
        }

        Step 'Publishing (Release)'
        $publish = Join-Path $Build 'publish'
        if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
        Run dotnet @('publish', 'Vispl.HRManagement.Host\Vispl.HRManagement.Host.csproj', '-c', 'Release', '-o', $publish, '--nologo', '-v', 'q')

        if ($DryRun) { Step 'Dry run: skipping database backup and IIS deploy' } else {
        Step 'Deploying to IIS'
        # Files that belong to this server, never to the build.
        $keepFiles = @('appsettings.Production.json', 'web.config', 'app_offline.htm')
        $keepDirs  = @('.data', '.data-protection-keys', 'logs')
        $rollback  = Join-Path $Build 'rollback'

        # robocopy exit codes below 8 mean success, so it is called directly rather than through Run.
        $null = robocopy $SitePath $rollback /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 /XD @keepDirs
        if ($LASTEXITCODE -ge 8) { throw "Could not save the current build for rollback (robocopy $LASTEXITCODE)" }

        Set-Content (Join-Path $SitePath 'app_offline.htm') '<!doctype html><title>Updating</title><h1>Kapson HRMS is updating</h1><p>Back in a minute.</p>' -Encoding utf8
        Start-Sleep -Seconds 5      # let IIS unload the app so its DLLs are unlocked

        $null = robocopy $publish $SitePath /MIR /NFL /NDL /NJH /NJS /NP /R:5 /W:2 /XF @keepFiles /XD @keepDirs
        $copyCode = $LASTEXITCODE
        Remove-Item (Join-Path $SitePath 'app_offline.htm') -Force
        if ($copyCode -ge 8) { throw "Copy to IIS failed (robocopy $copyCode)" }

        Step 'Health check'
        if (-not (Test-Site)) {
            Write-Host '   New build is not healthy - rolling back.' -ForegroundColor Yellow
            Set-Content (Join-Path $SitePath 'app_offline.htm') 'Rolling back' -Encoding utf8
            Start-Sleep -Seconds 5
            $null = robocopy $rollback $SitePath /MIR /NFL /NDL /NJH /NJS /NP /R:5 /W:2 /XF app_offline.htm /XD @keepDirs
            Remove-Item (Join-Path $SitePath 'app_offline.htm') -Force
            if (Test-Site) { throw 'Deploy rolled back: the new build failed its health check; the previous build is live again.' }
            throw 'Deploy failed AND rollback is unhealthy - check C:\inetpub\HRMS\logs.'
        }
        Write-Host '   /kapson_home answers 200 over HTTPS (local).' -ForegroundColor Green
        }

        Step "Smoke tests against $SiteUrl"
        $env:SMOKE_BASE_URL = $SiteUrl
        $filter = if ($env:SMOKE_USERNAME) { 'Category=Smoke' } else { 'Category=Smoke&FullyQualifiedName!~Implemented_endpoints_do_not_answer_not_implemented' }
        & dotnet test 'Vispl.HRManagement.Tests\Vispl.HRManagement.Tests.csproj' -c Release --nologo -v q --no-build --filter $filter
        if ($LASTEXITCODE -ne 0) { Write-Host '   Some smoke tests failed (site is live; see above).' -ForegroundColor Yellow; $exitCode = 2 }
    }

    if ($Apk -or $ApkOnly) {
        Step 'Building signed release APK'
        $sdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
        $jdk = Join-Path $env:LOCALAPPDATA 'Android\openjdk'
        $signing = Join-Path $RepoRoot 'android-signing'
        $keystore = Join-Path $signing 'kapsons-employee.keystore'
        if (-not (Test-Path $keystore)) { throw "Signing key not found: $keystore" }
        $env:KAPSONS_KS_PASS = ((Get-Content (Join-Path $signing 'keystore-password.txt') | Select-String 'Password') -split ': ')[1].Trim()

        # Android needs a higher version code for every update: minutes since 2026, above the
        # older yyMMddHH codes (26xxxxxx) so updates still install over those builds.
        $versionCode = 30000000 + [int][math]::Floor(((Get-Date).ToUniversalTime() - [datetime]'2026-01-01').TotalMinutes)
        $env:JAVA_HOME = $jdk
        $apksigner = Get-ChildItem (Join-Path $sdk 'build-tools') -Directory | Sort-Object Name -Descending | Select-Object -First 1 | ForEach-Object { Join-Path $_.FullName 'apksigner.bat' }
        $release = Join-Path $RepoRoot 'android-release'
        New-Item -ItemType Directory -Force $release | Out-Null

        # Both phone apps, signed with the same key. Each line of last-apk.txt is
        # "<gateway app key>|<apk path>|<version code>", read by the pipeline.
        $apps = @(
            @{ Key = 'hrms';    Project = 'Vispl.HRManagement.EmployeeApp\Vispl.HRManagement.EmployeeApp.csproj'; File = 'Kapsons-Employee' },
            @{ Key = 'hrms-hr'; Project = 'Vispl.HRManagement.Mobile\Vispl.HRManagement.Mobile.csproj';           File = 'Kapsons-HR' }
        )
        $built = @()
        try {
            foreach ($app in $apps) {
                Step "Building $($app.File) APK"
                $out = Join-Path $Build "apk-$($app.Key)"
                if (Test-Path $out) { Remove-Item $out -Recurse -Force }
                Run dotnet @('publish', $app.Project, '-f', 'net9.0-android', '-c', 'Release',
                    '-o', $out, '--nologo', '-v', 'q', "-p:AndroidSdkDirectory=$sdk", "-p:JavaSdkDirectory=$jdk", '-p:AndroidPackageFormat=apk',
                    "-p:ApplicationVersion=$versionCode", '-p:AndroidKeyStore=true', "-p:AndroidSigningKeyStore=$keystore",
                    '-p:AndroidSigningKeyAlias=kapsons', '-p:AndroidSigningKeyPass=env:KAPSONS_KS_PASS', '-p:AndroidSigningStorePass=env:KAPSONS_KS_PASS')

                $signed = Get-ChildItem $out -Filter '*-Signed.apk' | Select-Object -First 1
                if (-not $signed) { throw "$($app.File): the build produced no signed APK." }
                & $apksigner verify $signed.FullName
                if ($LASTEXITCODE -ne 0) { throw "$($app.File): APK signature check failed." }

                $final = Join-Path $release "$($app.File)-$versionCode.apk"
                Copy-Item $signed.FullName $final -Force
                $built += "$($app.Key)|$final|$versionCode"
                Write-Host "   $final  ($([math]::Round((Get-Item $final).Length / 1MB, 1)) MB, signature verified)" -ForegroundColor Green
            }

            # The Flutter apps, pointed at the same server as the MAUI apps and re-signed with the
            # same key. Each is skipped on its own when its folder or the Flutter SDK is missing.
            $flutter = @((Get-Command flutter.bat -ErrorAction SilentlyContinue).Source, 'C:\src\flutter\bin\flutter.bat') | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
            $flutterApps = @(
                @{ Key = 'hrms-flutter';    Folder = 'kapson_flutter_app';    File = 'Kapsons-Flutter' },
                @{ Key = 'hrms-hr-flutter'; Folder = 'kapson_hr_flutter_app'; File = 'Kapsons-HR-Flutter' }
            )
            # Java's NIO pipe uses a Unix socket in the temp folder and fails when TEMP is an 8.3
            # short path (C:\Users\ADMINI~1\...), which some shells set. Give it a plain folder.
            $env:JAVA_TOOL_OPTIONS = '-Djdk.net.unixdomain.tmpdir=C:\Windows\Temp'
            foreach ($fapp in $flutterApps) {
                $flutterApp = Join-Path $RepoRoot $fapp.Folder
                if (-not ((Test-Path (Join-Path $flutterApp 'pubspec.yaml')) -and $flutter)) {
                    Write-Host "   $($fapp.File): app folder or Flutter SDK not found - skipping." -ForegroundColor Yellow
                    continue
                }
                try {
                    Step "Building $($fapp.File) APK"
                    $clientFile = Join-Path $Kapson 'Vispl.HRManagement.Mobile.Client\Http\MobileApiClient.cs'
                    if ([IO.File]::ReadAllText($clientFile) -notmatch 'DefaultBaseAddress = "([^"]+)"') { throw 'could not read the server address from MobileApiClient.cs' }
                    $apiBase = $Matches[1]
                    $env:ANDROID_HOME = $sdk
                    Push-Location $flutterApp
                    try {
                        Run $flutter @('build', 'apk', '--release', "--dart-define=API_BASE_URL=$apiBase", "--build-number=$versionCode")
                    } finally { Pop-Location }
                    $unsigned = Join-Path $flutterApp 'build\app\outputs\flutter-apk\app-release.apk'
                    if (-not (Test-Path $unsigned)) { throw 'the build produced no APK.' }
                    $final = Join-Path $release "$($fapp.File)-$versionCode.apk"
                    & $apksigner sign --ks $keystore --ks-key-alias kapsons --ks-pass env:KAPSONS_KS_PASS --key-pass env:KAPSONS_KS_PASS --out $final $unsigned
                    if ($LASTEXITCODE -ne 0) { throw 'signing failed.' }
                    & $apksigner verify $final
                    if ($LASTEXITCODE -ne 0) { throw 'APK signature check failed.' }
                    $built += "$($fapp.Key)|$final|$versionCode"
                    Write-Host "   $final  ($([math]::Round((Get-Item $final).Length / 1MB, 1)) MB, signature verified)" -ForegroundColor Green
                } catch {
                    # A Flutter failure must not hold back the MAUI apps or the other Flutter app.
                    Write-Host "   $($fapp.File) APK skipped: $($_.Exception.Message)" -ForegroundColor Yellow
                    $exitCode = 2
                }
            }
            Remove-Item Env:JAVA_TOOL_OPTIONS -ErrorAction SilentlyContinue
        } finally { Remove-Item Env:KAPSONS_KS_PASS -ErrorAction SilentlyContinue }

        Set-Content (Join-Path $Build 'last-apk.txt') $built -Encoding utf8   # read by the pipeline
    }

    Step "Done in $([int]$sw.Elapsed.TotalSeconds)s"
}
catch {
    Write-Host ''
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 1
}
finally {
    Pop-Location -ErrorAction SilentlyContinue
    Stop-Transcript | Out-Null
    Write-Host "Log: $Log"
    if (-not $NoPause) { try { Read-Host 'Press Enter to close' | Out-Null } catch { } }
}
exit $exitCode
