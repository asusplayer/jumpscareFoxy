# Build and Package Script for JumpscareFoxy Mod
# This script builds the mod and creates a Thunderstore-ready package

Write-Host "`n=== JumpscareFoxy Build & Package Script ===" -ForegroundColor Cyan

# Get version from manifest.json
$manifestPath = ".\manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Host "Error: manifest.json not found!" -ForegroundColor Red
    exit 1
}

$manifest = Get-Content $manifestPath | ConvertFrom-Json
$version = $manifest.version_number
$modName = $manifest.name

Write-Host "Building $modName v$version..." -ForegroundColor Yellow

# Clean old build artifacts
Write-Host "Cleaning old packages..." -ForegroundColor Yellow
Remove-Item "*.zip" -Force -ErrorAction SilentlyContinue
Remove-Item ".\package-fixed" -Recurse -Force -ErrorAction SilentlyContinue

# Build the project
Write-Host "Building Release configuration..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Create package structure
Write-Host "Creating package structure..." -ForegroundColor Yellow
$packageDir = ".\package-fixed"
$pluginDir = "$packageDir\plugins\$modName"
New-Item -ItemType Directory -Path "$pluginDir\assets\frames" -Force | Out-Null

# Copy files to package
Write-Host "Copying files..." -ForegroundColor Yellow
Copy-Item ".\bin\Release\netstandard2.1\jumpscareFoxy.dll" "$pluginDir\"
Copy-Item ".\assets\jumpscare.wav" "$pluginDir\assets\"
Copy-Item ".\assets\frames\*.png" "$pluginDir\assets\frames\"
Copy-Item ".\manifest.json" "$packageDir\"
Copy-Item ".\icon.png" "$packageDir\"
Copy-Item ".\README.md" "$packageDir\"

# Create zip package
Write-Host "Creating Thunderstore package..." -ForegroundColor Yellow
$zipName = "$modName-$version.zip"
Compress-Archive -Path "$packageDir\*" -DestinationPath $zipName -CompressionLevel Optimal

# Cleanup
Remove-Item $packageDir -Recurse -Force

# Display results
Write-Host ""
Write-Host "Package created successfully!" -ForegroundColor Green
$zipInfo = Get-Item $zipName
Write-Host "Package: $($zipInfo.Name)" -ForegroundColor Cyan
Write-Host "Size: $([math]::Round($zipInfo.Length/1KB, 2)) KB" -ForegroundColor White
Write-Host "Path: $($zipInfo.FullName)" -ForegroundColor Gray
Write-Host ""
Write-Host "Ready to upload to Thunderstore!" -ForegroundColor Green
Write-Host ""
