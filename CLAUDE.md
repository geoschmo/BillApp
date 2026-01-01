# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Build Commands

```bash
dotnet restore            # Restore NuGet packages
dotnet build BillApp.sln  # Build the solution
dotnet run --project src/BillApp/BillApp.csproj  # Run the application
```

## Project Overview

BillApp is a Windows desktop application for personal finance management built with:
- **WPF** (.NET 8/10) for the UI
- **MVVM pattern** using CommunityToolkit.Mvvm
- **LiteDB** for local data storage
- **AES + DPAPI** for encryption (Phase 3)

## Solution Structure

```
BillApp/
├── src/
│   ├── BillApp/                    # WPF Application (Views, ViewModels, Services)
│   ├── BillApp.Core/               # Business logic (Models, Interfaces, Enums)
│   └── BillApp.Infrastructure/     # Data access (LiteDB, Repositories, Security)
└── tests/                          # Unit tests
```

## Architecture Notes

- **Views** are XAML files in `src/BillApp/Views/`
- **ViewModels** handle logic and are in `src/BillApp/ViewModels/`
- **DataTemplates** in `App.xaml` map ViewModels to Views (like Angular routes)
- **NavigationService** handles view navigation (like Angular Router)
- **DI Container** is set up in `App.xaml.cs`

## Key Files

- `src/BillApp/App.xaml.cs` - DI container setup
- `src/BillApp/Services/NavigationService.cs` - View navigation
- `src/BillApp.Infrastructure/Data/LiteDbContext.cs` - Database access
- `src/BillApp.Core/Interfaces/Repositories/IRepository.cs` - Data access contract

## Security Guidelines

- NEVER commit encryption keys (*.key files)
- NEVER commit database files (*.db)
- NEVER commit backup files (*.billbackup)
- Sensitive data is stored encrypted using DPAPI-protected AES keys
