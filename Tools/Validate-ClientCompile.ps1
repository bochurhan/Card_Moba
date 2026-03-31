param(
    [string]$UnityPath = "D:\Program Files\Unity\2022.3.62f3c1\Editor\Unity.exe",
    [string]$ProjectPath = (Join-Path $PSScriptRoot "..\Client"),
    [string]$LogPath = (Join-Path $PSScriptRoot "..\Client\Logs\batch-compile.log"),
    [string]$FallbackEditorLogPath = (Join-Path $env:LOCALAPPDATA "Unity\Editor\Editor.log"),
    [switch]$AllowRunningEditor,
    [switch]$ShowLogOnSuccess
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable not found: $UnityPath"
}

$runningUnity = Get-Process -Name "Unity" -ErrorAction SilentlyContinue
if ($runningUnity -and -not $AllowRunningEditor) {
    throw "A Unity Editor process is already running. Close Unity before batch validation, or rerun with -AllowRunningEditor if you intentionally want to inspect the shared Editor.log."
}

$fallbackLineCountBefore = 0
if (Test-Path -LiteralPath $FallbackEditorLogPath) {
    $fallbackLineCountBefore = (Get-Content -Path $FallbackEditorLogPath).Count
}

$resolvedProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$resolvedLogPath = [System.IO.Path]::GetFullPath($LogPath)
$logDirectory = Split-Path -Path $resolvedLogPath -Parent

if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory | Out-Null
}

$unityArgs = @(
    "-batchmode"
    "-nographics"
    "-quit"
    "-projectPath", $resolvedProjectPath
    "-logFile", $resolvedLogPath
)

Write-Host "[compile] Unity: $UnityPath"
Write-Host "[compile] Project: $resolvedProjectPath"
Write-Host "[compile] Log: $resolvedLogPath"
Write-Host "[compile] Starting batch compile..."

$process = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -Wait -PassThru
$exitCode = $process.ExitCode

$effectiveLogPath = $resolvedLogPath
if (-not (Test-Path -LiteralPath $effectiveLogPath)) {
    if (Test-Path -LiteralPath $FallbackEditorLogPath) {
        $effectiveLogPath = $FallbackEditorLogPath
    }
    else {
        throw "Unity finished without producing a log file: $resolvedLogPath"
    }
}

Write-Host "[compile] Effective log: $effectiveLogPath"
$logLines = Get-Content -Path $effectiveLogPath
if ($effectiveLogPath -eq $FallbackEditorLogPath -and $fallbackLineCountBefore -gt 0 -and $logLines.Count -gt $fallbackLineCountBefore) {
    $logLines = $logLines | Select-Object -Skip $fallbackLineCountBefore
}

if ($exitCode -eq 0) {
    Write-Host "[compile] Success."
    if ($ShowLogOnSuccess) {
        $logLines | Select-Object -Last 40
    }

    exit 0
}

Write-Host "[compile] Failed with exit code $exitCode."

$summary = $logLines | Where-Object {
    $_ -match "error CS\d+:" -or
    $_ -match "Scripts have compiler errors" -or
    $_ -match "Package Manager" -or
    $_ -match "UPM error" -or
    $_ -match "error:" -or
    $_ -match "Compilation failed"
}

if ($summary.Count -gt 0) {
    Write-Host "[compile] Summary:"
    $summary | Select-Object -Unique | Select-Object -Last 60
}
else {
    Write-Host "[compile] Tail of Unity log:"
    $logLines | Select-Object -Last 80
}

exit $exitCode
