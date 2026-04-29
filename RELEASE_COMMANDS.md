# ActiveManager - Release Commands

Commands for building and publishing ActiveManager as a standalone executable.

---

## .NET 8 LTS (Recommended for Windows Server)

### Prerequisites

Update `ActiveManager.csproj`:

```xml
<TargetFramework>net8.0-windows</TargetFramework>
```

Update package versions:

- `System.DirectoryServices` → `8.0.0`
- `System.DirectoryServices.AccountManagement` → `8.0.0`

### Build (Self-Contained, Single File)

```powershell
dotnet publish ADUserManager/ActiveManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

**Output:** `ActiveManager/bin/Release/net8.0-windows/win-x64/publish/`

### Create ZIP

```powershell
Compress-Archive -Path "ADUserManager/bin/Release/net8.0-windows/win-x64/publish/*" -DestinationPath "ActiveManager.zip" -Force
```

---

## .NET 10 (Preview/Experimental)

### Prerequisites

Update `ActiveManager.csproj`:

```xml
<TargetFramework>net10.0-windows</TargetFramework>
```

Update package versions:

- `System.DirectoryServices` → `9.0.4`
- `System.DirectoryServices.AccountManagement` → `9.0.4`

### Build (Self-Contained, Single File)

```powershell
dotnet publish ActiveManager/ActiveManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

**Output:** `ActiveManager/bin/Release/net10.0-windows/win-x64/publish/`

### Create ZIP

```powershell
Compress-Archive -Path "ActiveManager\bin\Release\net10.0-windows\win-x64\publish\*" -DestinationPath "ActiveManager-Net10.zip" -Force
```

---

## GitHub Release (using gh CLI)

```powershell
gh release create v1.0.0 ActiveManager-Net8.zip --title "ActiveManager v1.0.0" --notes "Release notes here"
```

---

## All In One (.NET 8)

```powershell
dotnet publish ActiveManager/ActiveManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true; Compress-Archive -Path "ActiveManager\bin\Release\net8.0-windows\win-x64\publish\*" -DestinationPath "ActiveManager-Net8.zip" -Force
```
