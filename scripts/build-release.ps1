param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$artifacts = Join-Path $root "artifacts"
$selfDir = Join-Path $artifacts "self-contained"
$fddDir = Join-Path $artifacts "framework-dependent"

if (Test-Path $artifacts) {
    Remove-Item $artifacts -Recurse -Force
}

New-Item -ItemType Directory -Path $selfDir, $fddDir -Force | Out-Null

Write-Host "Publishing self-contained build..."
dotnet publish ClevoRgbLite.csproj -c Release -r win-x64 --self-contained true --nologo -o $selfDir
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed." }

Write-Host "Publishing framework-dependent build..."
dotnet publish ClevoRgbLite.csproj -c Release -r win-x64 --self-contained false --nologo -o $fddDir
if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed." }

# Vendor binaries are proprietary and must not be redistributed.
$vendorFiles = @(
    "InsydeDCHU.dll",
    "GetProductdll.dll",
    "msvcp140.dll",
    "vcruntime140.dll",
    "vcruntime140_1.dll"
)

foreach ($dir in @($selfDir, $fddDir)) {
    foreach ($file in $vendorFiles) {
        Remove-Item (Join-Path $dir $file) -Force -ErrorAction SilentlyContinue
    }
    Get-ChildItem $dir -Filter "*.pdb" -File | Remove-Item -Force
    Copy-Item (Join-Path $root "docs\INSTALL-RU.txt") (Join-Path $dir "README-RU.txt") -Force
    Copy-Item (Join-Path $root "LICENSE") (Join-Path $dir "LICENSE.txt") -Force
}

$selfZip = Join-Path $artifacts "ClevoRGB-$Version-win-x64.zip"
$fddZip = Join-Path $artifacts "ClevoRGB-$Version-win-x64-framework-dependent.zip"

Write-Host "Creating archives..."
Compress-Archive -Path (Join-Path $selfDir "*") -DestinationPath $selfZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $fddDir "*") -DestinationPath $fddZip -CompressionLevel Optimal

Get-FileHash $selfZip, $fddZip -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" } |
    Set-Content (Join-Path $artifacts "SHA256SUMS.txt")

Write-Host ""
Write-Host "Done:"
Get-ChildItem $artifacts -File | Select-Object Name, Length
