# 1. Complete nuclear option - remove ALL dotnet tool data
Remove-Item -Recurse -Force "$env:USERPROFILE\.dotnet\tools" -ErrorAction SilentlyContinue

# 2. Recreate the tools directory
mkdir "$env:USERPROFILE\.dotnet\tools"

# 3. Clear all NuGet sources
dotnet nuget locals all --clear

# 4. Remove global NuGet cache for xkode specifically
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\xkode" -ErrorAction SilentlyContinue

# 5. Go to your project
cd C:\Work\Lab\test\xkode\src\XKode

# 6. Verify the version in csproj
Select-String -Path .\XKode.csproj -Pattern "<Version>"

# If it shows 0.2.0, manually edit it:
# Open XKode.csproj in notepad
notepad .\XKode.csproj
# Change line 11 to: <Version>0.3.0</Version>
# Save and close

# 7. Clean everything
dotnet clean
Remove-Item -Recurse -Force .\bin -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\nupkg -ErrorAction SilentlyContinue

# 8. Build fresh
dotnet restore
dotnet build -c Release

# 9. Pack with explicit version
dotnet pack -c Release -o .\nupkg /p:Version=0.3.0

# 10. Check what was created
dir .\nupkg

# 11. Install with explicit version and force
dotnet tool install --global --add-source .\nupkg XKode --version 0.3.0

# 12. Test
xkode --version
xkode --help