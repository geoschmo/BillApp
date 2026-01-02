# BillApp

A Windows desktop application for personal finance management, built with WPF and .NET. Designed to replace spreadsheet-based bill tracking with a secure, user-friendly local application.

## Features

### Current (Implemented)

**Bill Management**
- Create, edit, and delete bills
- Track amount due, amount paid, and balance
- Due date tracking with overdue highlighting
- Categorize bills (Utilities, Housing, Insurance, etc.)
- Payment status tracking (Pending, Paid, Overdue)
- Recurring bills (Weekly, Bi-weekly, Monthly, Quarterly, Semi-annually, Annually)
- Auto-create next occurrence when a recurring bill is marked as paid
- Inline editing directly in the bill list
- Filter and search bills

**Security**
- AES-256 encrypted database (LiteDB with password protection)
- Encryption key protected by Windows DPAPI (tied to user account)
- Automatic migration of unencrypted data on upgrade
- Field-level encryption service available for sensitive data

### Planned Features

| Phase | Feature | Description |
|-------|---------|-------------|
| 4 | Accounts | Bank accounts, credit cards, asset tracking |
| 5 | Budget | Budget creation, spending categories, progress tracking |
| 6 | Recurring Service | Background service for auto-creating upcoming recurring bills |
| 7 | Secure Notes | Encrypted storage for passwords, PINs, and sensitive notes |
| 8 | Dashboard & Reports | Financial overview, charts, spending analysis |
| 9 | Notifications | Due date reminders and alerts |
| 10 | Backup & Restore | Encrypted export/import for cloud backup (Google Drive, etc.) |

## Technology Stack

- **UI Framework:** WPF (.NET 8/10)
- **Architecture:** MVVM with CommunityToolkit.Mvvm
- **Database:** LiteDB (embedded NoSQL)
- **Encryption:** AES-256 + Windows DPAPI
- **Dependency Injection:** Microsoft.Extensions.DependencyInjection

## Project Structure

```
BillApp/
├── src/
│   ├── BillApp/                    # WPF Application
│   │   ├── Views/                  # XAML UI files
│   │   ├── ViewModels/             # MVVM view models
│   │   ├── Services/               # Navigation, etc.
│   │   └── Converters/             # Value converters
│   ├── BillApp.Core/               # Business Logic
│   │   ├── Models/                 # Entity classes
│   │   ├── Enums/                  # Status, frequency enums
│   │   └── Interfaces/             # Repository & service contracts
│   └── BillApp.Infrastructure/     # Data Access
│       ├── Data/                   # LiteDB context
│       ├── Repositories/           # Data access implementations
│       └── Security/               # Encryption, key management
└── tests/                          # Unit tests (planned)
```

## Getting Started

### Prerequisites

- Windows 10/11
- .NET 8 SDK or later
- Visual Studio 2022 (recommended) or VS Code

### Build & Run

```bash
# Clone the repository
git clone https://github.com/yourusername/BillApp.git
cd BillApp

# Restore dependencies
dotnet restore

# Build
dotnet build BillApp.sln

# Run
dotnet run --project src/BillApp/BillApp.csproj
```

Or open `BillApp.sln` in Visual Studio and press F5.

### Data Location

- **Database:** `%LocalAppData%\BillApp\billapp.db`
- **Encryption Key:** `%LocalAppData%\BillApp\key.dat`

> **Note:** The encryption key is protected by your Windows user account. If you reinstall Windows or move to a new PC, you'll need to restore from an encrypted backup (Phase 10).

## Architecture Notes

This project uses patterns familiar to Angular developers:

| Angular Concept | WPF Equivalent |
|-----------------|----------------|
| Components | Views (XAML) + ViewModels |
| Services | Services (singleton/transient via DI) |
| Router | NavigationService |
| Route Config | DataTemplates in App.xaml |
| Pipes | Value Converters |
| Two-way Binding | `{Binding ..., Mode=TwoWay}` |

## Security

- Database is encrypted at rest using LiteDB's built-in AES encryption
- Encryption key is protected by Windows DPAPI (CurrentUser scope)
- No sensitive data is stored in plain text
- Key files and database files are excluded from git

## License

MIT License - See [LICENSE](LICENSE) for details.

## Contributing

This is a personal project for learning WPF and desktop development. Contributions and suggestions are welcome!
