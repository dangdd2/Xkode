Write-Host "==== XKODE REINSTALL START ====" -ForegroundColor Cyan

# Step 1: Uninstall (ignore error if not installed)
Write-Host "Uninstalling existing tool..."
dotnet tool uninstall --global xkode 2>$null

# Step 2: Clean old nupkg folder
if (Test-Path "./nupkg") {
    Remove-Item "./nupkg" -Recurse -Force
}

# Step 3: Pack
Write-Host "Packing release..."
dotnet pack -c Release -o ./nupkg

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Pack failed!" -ForegroundColor Red
    exit 1
}

# Step 4: Install
Write-Host "Installing tool..."
dotnet tool install --global --add-source ./nupkg XKode

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Install failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ XKODE successfully reinstalled!" -ForegroundColor Green
Write-Host "Version:"
dotnet tool list -g | Select-String "xkode"

Write-Host "==== DONE ====" -ForegroundColor Cyan
