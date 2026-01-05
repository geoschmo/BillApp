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
- **LiteDB** for local data storage (encrypted)
- **AES + DPAPI** for database and field-level encryption

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
- **Settings** are persisted via `ISettingsService` to JSON

## Key Files

- `src/BillApp/App.xaml.cs` - DI container setup and app startup
- `src/BillApp/Services/NavigationService.cs` - View navigation
- `src/BillApp/Services/SettingsService.cs` - User preferences (window size, column widths)
- `src/BillApp.Infrastructure/Data/LiteDbContext.cs` - Database access
- `src/BillApp.Infrastructure/Security/EncryptionService.cs` - Field-level encryption
- `src/BillApp.Infrastructure/Services/BackupService.cs` - Backup/restore with encryption
- `src/BillApp.Infrastructure/Services/CsvImportService.cs` - CSV import with validation
- `src/BillApp.Core/Interfaces/Repositories/IRepository.cs` - Data access contract

## Current Features

- **Bills**: CRUD, inline editing, recurring bills, status tracking, category/account linking, pay method tracking
- **Accounts**: Bank accounts, credit cards, loans with encrypted credentials, payment account designation
- **Backup/Restore**: Password-protected encrypted backups, portable across machines
- **Import**: CSV import with validation, add-to-existing or replace-all modes
- **Security**: Database encryption (AES), DPAPI key protection, field-level encryption
- **Settings**: Window size/position, column widths, backup settings persisted

## Data Models

- `Bill` - Payment tracking with status, due date, recurrence, linked category/account, payment method (PaymentAccountId, IsCashPayment), confirmation number
- `Account` - Financial accounts with encrypted credentials (AccountNumber, Username, Password), IsPaymentAccount flag
- `Category` - Bill categorization (seeded with defaults)

## Import CSV Template

```
AccountName,AccountNumber,InterestRate,AccountType,IsActive,IsPaymentAccount,Frequency,AmountDue,AmountPaid,Balance,Confirmation,DueDate,PaidDate,PaymentMethod
```

- **Required columns**: AccountName, DueDate
- **Frequency values**: None, Weekly, BiWeekly, Monthly, Quarterly, Annually
- **AccountType values**: Checking, Savings, CreditCard, Loan, Other
- **PaymentMethod**: "Cash" or account name (auto-creates if not found)

## Security Guidelines

- NEVER commit encryption keys (*.key files)
- NEVER commit database files (*.db)
- NEVER commit backup files (*.billbackup)
- Sensitive data is stored encrypted using DPAPI-protected AES keys
- Account credentials use field-level encryption via IEncryptionService
