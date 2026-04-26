param(
    [switch]$SkipGodotSmoke
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Push-Location $ProjectRoot
try {
    dotnet test tests\Sim.Tests\Sim.Tests.csproj
    dotnet build CreaturesReborn.csproj

    rg -n "TODO|TBD|placeholder" docs\superpowers src\Sim
    $markerExit = $LASTEXITCODE
    if ($markerExit -eq 0) {
        throw "Core v2 marker scan found unresolved marker strings."
    }
    if ($markerExit -gt 1) {
        throw "Core v2 marker scan failed with exit code $markerExit."
    }

    if (-not $SkipGodotSmoke) {
        $godotAvailable = -not [string]::IsNullOrWhiteSpace($env:GODOT_BIN)
        if (-not $godotAvailable) {
            $godotAvailable = (Get-Command godot -ErrorAction SilentlyContinue) -ne $null -or
                (Get-Command godot4 -ErrorAction SilentlyContinue) -ne $null
        }

        if ($godotAvailable) {
            & tools\run_godot_smoke.ps1
        }
        else {
            Write-Warning "Godot executable not found. Set GODOT_BIN or pass -SkipGodotSmoke for an explicit non-Godot gate."
        }
    }
}
finally {
    Pop-Location
}
