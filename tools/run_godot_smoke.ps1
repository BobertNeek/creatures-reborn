param(
    [string]$GodotPath = $env:GODOT_BIN,
    [string]$OutputDir = "_dev_screenshots\smoke",
    [switch]$KeepLogs
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $candidate = Get-Command godot -ErrorAction SilentlyContinue
    if ($candidate -eq $null) {
        $candidate = Get-Command godot4 -ErrorAction SilentlyContinue
    }
    if ($candidate -ne $null) {
        $GodotPath = $candidate.Source
    }
}

if ([string]::IsNullOrWhiteSpace($GodotPath) -or -not (Test-Path -LiteralPath $GodotPath)) {
    throw "Godot executable not found. Set GODOT_BIN or pass -GodotPath."
}

$outRoot = Join-Path $ProjectRoot $OutputDir
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

$scenes = @(
    @{ Name = "treehouse"; Path = "res://scenes/Treehouse.tscn" },
    @{ Name = "norn_colony"; Path = "res://scenes/NornColony.tscn" }
)

foreach ($scene in $scenes) {
    $png = Join-Path $outRoot "$($scene.Name).png"
    $log = Join-Path $outRoot "$($scene.Name).log"
    $args = @(
        "--headless",
        "--path", $ProjectRoot,
        $scene.Path,
        "--screenshot=$png"
    )

    Write-Host "Running Godot smoke for $($scene.Path)"
    $stdout = "$log.stdout"
    $stderr = "$log.stderr"
    $argumentLine = ($args | ForEach-Object {
        $arg = $_ -replace '"', '\"'
        if ($arg -match "\s") { '"' + $arg + '"' } else { $arg }
    }) -join " "
    $process = Start-Process `
        -FilePath $GodotPath `
        -ArgumentList $argumentLine `
        -WorkingDirectory $ProjectRoot `
        -NoNewWindow `
        -Wait `
        -PassThru `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr
    $combined = @()
    if (Test-Path -LiteralPath $stdout) { $combined += Get-Content -LiteralPath $stdout }
    if (Test-Path -LiteralPath $stderr) { $combined += Get-Content -LiteralPath $stderr }
    $combined | Tee-Object -FilePath $log
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    $exitCode = $process.ExitCode
    if ($exitCode -ne 0) {
        throw "Godot smoke failed for $($scene.Path) with exit code $exitCode"
    }

    $text = Get-Content -LiteralPath $log -Raw
    if ($text -notmatch "Simulation world initialised") {
        throw "Smoke log missing Simulation world initialised for $($scene.Path)"
    }
    if ($text -notmatch "Saved PNG") {
        throw "Smoke log missing Saved PNG for $($scene.Path)"
    }
    if (-not (Test-Path -LiteralPath $png)) {
        throw "Smoke screenshot missing: $png"
    }

    $unexpectedErrors = $text -split "`r?`n" |
        Where-Object {
            $_ -match "ERROR:" -and
            $_ -notmatch "ProgressDialog" -and
            $_ -notmatch "Condition `"!windows\.has"
        }
    if ($unexpectedErrors.Count -gt 0) {
        throw "Unexpected Godot errors in $($scene.Path): $($unexpectedErrors -join '; ')"
    }

    $assetWarnings = $text -split "`r?`n" |
        Where-Object {
            $_ -match "WARNING:" -and
            ($_ -match "Invalid UID" -or $_ -match "external resource")
        }
    if ($assetWarnings.Count -gt 0) {
        throw "Unexpected Godot asset warnings in $($scene.Path): $($assetWarnings -join '; ')"
    }

    if (-not $KeepLogs) {
        Remove-Item -LiteralPath $log -Force
    }
}

Write-Host "Godot smoke passed. Screenshots written to $outRoot"
