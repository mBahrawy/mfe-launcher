# Builds dist\mfe-launcher.exe (.NET Framework 4.8, nothing to install on Windows 10/11).
# -Dest also copies it into a workspace folder, e.g. .\build.ps1 -Dest C:\code\my-nx-workspace
param([string]$Dest)

$ErrorActionPreference = 'Stop'
dotnet build "$PSScriptRoot\src\MfeLauncher.csproj" -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = "$PSScriptRoot\src\bin\Release\net48\mfe-launcher.exe"
New-Item -ItemType Directory -Force "$PSScriptRoot\dist" | Out-Null
Copy-Item $exe "$PSScriptRoot\dist" -Force
Write-Output "Built $PSScriptRoot\dist\mfe-launcher.exe"

if ($Dest) {
    Copy-Item $exe $Dest -Force
    Write-Output "Copied mfe-launcher.exe to $Dest"
}
