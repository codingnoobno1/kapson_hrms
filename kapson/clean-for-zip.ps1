[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$rootPrefix = $projectRoot.TrimEnd('\') + '\'

if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
    throw "Project root does not exist: $projectRoot"
}

Write-Host "HR Management ZIP cleanup" -ForegroundColor Cyan
Write-Host "Project root: $projectRoot"
Write-Host "Searching for bin and obj directories..."

$targets = @(
    Get-ChildItem -LiteralPath $projectRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq 'bin' -or $_.Name -eq 'obj' } |
        Sort-Object { $_.FullName.Length } -Descending
)

if ($targets.Count -eq 0) {
    Write-Host "No bin or obj directories were found. The project is already clean." -ForegroundColor Green
    exit 0
}

Write-Host "Found $($targets.Count) directories:" -ForegroundColor Yellow
foreach ($target in $targets) {
    Write-Host "  $($target.FullName.Substring($rootPrefix.Length))"
}

if (-not $Force) {
    $confirmation = Read-Host "Type YES to permanently delete these directories"
    if ($confirmation -cne 'YES') {
        Write-Host "Cleanup cancelled. Nothing was deleted." -ForegroundColor Yellow
        exit 0
    }
}

$removedCount = 0
$failedTargets = [System.Collections.Generic.List[string]]::new()

foreach ($target in $targets) {
    try {
        $resolvedTarget = [System.IO.Path]::GetFullPath($target.FullName)
        $isExpectedName = $target.Name -eq 'bin' -or $target.Name -eq 'obj'
        $isInsideProject = $resolvedTarget.StartsWith(
            $rootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)
        $isReparsePoint = ($target.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0

        if (-not $isExpectedName -or -not $isInsideProject) {
            throw "Safety validation rejected target: $resolvedTarget"
        }

        if ($isReparsePoint) {
            throw "Safety validation rejected reparse-point directory: $resolvedTarget"
        }

        if (Test-Path -LiteralPath $resolvedTarget -PathType Container) {
            Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
            $removedCount++
            Write-Host "Removed: $resolvedTarget" -ForegroundColor DarkGray
        }
    }
    catch {
        $failedTargets.Add("$($target.FullName) - $($_.Exception.Message)")
    }
}

Write-Host ""
Write-Host "Removed $removedCount of $($targets.Count) bin/obj directories." -ForegroundColor Green

if ($failedTargets.Count -gt 0) {
    Write-Host "The following directories could not be removed:" -ForegroundColor Red
    foreach ($failure in $failedTargets) {
        Write-Host "  $failure" -ForegroundColor Red
    }
    Write-Host "Stop any running dotnet processes using these folders, then run the script again." -ForegroundColor Yellow
    exit 1
}

Write-Host "Cleanup complete. The source tree is ready to ZIP." -ForegroundColor Green
exit 0
