# ActiveManager

ActiveManager is a Windows desktop application for day-to-day Active Directory user administration. It provides a WPF UI for searching users, creating accounts, editing profile details and groups, resetting passwords, terminating users, restoring captured account state, and reviewing audit/history data.

The app is built for helpdesk and IT administrators who need a safer workflow around AD changes. It keeps local mementos for rollback, can store audit and backup history in SQL Server, and supports English/Lithuanian UI text.

## Features

- Search and inspect on-premises Active Directory users.
- Create users with generated temporary passwords and optional group assignment.
- Edit names, email, department, title, description, account state, OU, and group membership.
- Reset passwords and record password reset history.
- Terminate users through a controlled workflow: disable account, move OU, rotate password, remove groups, set expiration, and create backups.
- Restore users from local mementos or database backups.
- Dashboard with AD/database health, risk metrics, recent actions, and alerts.
- SQL Server-backed audit logs, termination history, password reset history, and AD user backup snapshots.
- Optional SMTP email notifications for termination and rollback events.

## Requirements

- Windows 10/11 or Windows Server with desktop experience.
- .NET 8 SDK for development.
- Active Directory access with permissions for the actions you run.
- SQL Server 2019+ or SQL Server Express for shared audit/backup storage.
- Administrator privileges when launching the app, because AD management operations require elevated access.

## Project Structure

- `ADUserManager/` - WPF application source.
- `ActiveManager.Tests/` - xUnit unit tests.
- `ADUserManager.sln` - Visual Studio solution.
- `install.ps1` - release installer script.
- `RELEASE_COMMANDS.md` - release build/publish helper commands.

## Configuration

Settings are stored per Windows user under:

```text
%APPDATA%\ActiveManager\appsettings.json
```

Most settings can be changed from the Settings page in the app.

### Database

The current application uses SQL Server through `Microsoft.Data.SqlClient`. Default settings are:

- Server: `localhost`
- Port: `1433`
- Database: `ActiveManager`
- User: `sa`

You can also paste a full SQL Server connection string in Settings. If the configured account has permission, the app attempts to create the target database and required tables automatically on first connection.

### Email

SMTP settings are optional. Enable email notifications in Settings and configure server, port, sender, credentials, recipients, and which events should send notifications.

## Build

Restore and build:

```powershell
dotnet restore ADUserManager.sln
dotnet build ADUserManager.sln -c Release
```

Publish a self-contained Windows x64 executable:

```powershell
dotnet publish ADUserManager\ActiveManager.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The publish output is created under:

```text
ADUserManager\bin\Release\net8.0-windows\win-x64\publish
```

## Test

Run the unit tests:

```powershell
dotnet test ActiveManager.Tests\ActiveManager.Tests.csproj
```

## Install

For release installs, run:

```powershell
irm https://raw.githubusercontent.com/vokenboy/ADUserManager/main/install.ps1 | iex
```

Manual install:

1. Download `ActiveManager.zip` from the latest release.
2. Extract it to a permanent folder.
3. Run `ActiveManager.exe` as administrator.

## Notes

- The old API project and MySQL/PC-inventory database dumps are not part of the current product.
- Runtime state such as `crash.log`, `memento_states.json`, local .NET caches, build outputs, and release zip files should not be committed.
- Azure AD service classes exist in the source, but the finished workflow currently targets on-premises Active Directory first.
