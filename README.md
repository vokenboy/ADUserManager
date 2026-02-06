# ADUserManager

A Windows application for managing Active Directory users and groups.

## Installation

### Quick Install (One-Liner)

Host `install.ps1` on a public server (e.g., GitHub Gist, S3, your website), then users can run:

```powershell
powershell -ExecutionPolicy Bypass -Command "irm https://your-hosted-url/install.ps1 | iex"
```

This will:
- Download and extract the latest release
- Add ADUserManager to your PATH
- Launch the application

You can then run `ADUserManager` from any terminal.

### Manual Install:
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
