# Windows build and run

ARVREL is intended to sit beside the reusable ARIEC61850 engine:

```text
C:\Git\
├── ARIEC61850\
└── arvrel\
```

## Recommended commands

The repository includes `.cmd` entry points that run the PowerShell implementation with a process-local execution-policy bypass. They do not change the machine or current-user execution policy.

```powershell
cd C:\Git\arvrel
.\scripts\verify-sibling.cmd
.\scripts\build.cmd
.\scripts\run.cmd
```

The equivalent one-time PowerShell command is:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-sibling.ps1
```

Avoid changing the computer-wide execution policy only to run this repository.

## Direct .NET commands

```powershell
dotnet restore .\ARVREL.sln
dotnet build .\ARVREL.sln -c Release --no-restore
dotnet test .\ARVREL.sln -c Release --no-build
dotnet run --project .\src\Arvrel.App\Arvrel.App.csproj -c Release
```

## Troubleshooting

### Sibling repository is not detected

Confirm these files exist:

```text
C:\Git\ARIEC61850\src\AR.Iec61850\AR.Iec61850.csproj
C:\Git\ARIEC61850\src\AR.Iec61850.Transports.Npcap\AR.Iec61850.Transports.Npcap.csproj
```

### PowerShell reports that a script is not digitally signed

Use the `.cmd` entry point. It applies `-ExecutionPolicy Bypass` only to the child PowerShell process used for that command.

### Clean rebuild

```powershell
dotnet clean .\ARVREL.sln -c Release
dotnet restore .\ARVREL.sln
dotnet build .\ARVREL.sln -c Release --no-restore
```
