# ADUserManager

A Windows application for managing Active Directory users and groups.

## Installation

### Greitas Diegimas (Viena Eilutė)

Paleiskite šią komandą PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/vokenboy/ADUserManager/main/install.ps1 | iex"
```

Tai atliks:
- Atsisiųs ir išskleis naujausią versiją
- Pridės ADUserManager į PATH
- Paleis programą

Tada galėsite paleisti `ADUserManager` iš bet kurio terminalo.

### Rankinis Diegimas:
1. Atsisiųskite `ADUserManager.zip` iš [naujausio leidimo](../../releases/latest)
2. Išskleiskite į aplanką
3. Paleiskite `ADUserManager.exe`

## Reikalavimai

- Windows 10/11
- .NET 10 Runtime (įtrauktas į vieną failą)
- Active Directory prieiga

## Kūrimas

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## License

MIT
