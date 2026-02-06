# ADUserManager

A Windows application for managing Active Directory users and groups.

## Installation

Install via PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/vokenboy/ADUserManager/main/install.ps1 | iex"
```

Or manually:
1. Download `ADUserManager.zip` from the [latest release](../../releases/latest)
2. Extract to a folder
3. Run `ADUserManager.exe`

## Requirements

- Windows 10/11
- .NET 10 Runtime (included in single-file build)
- Active Directory access

## Building

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## License

MIT
