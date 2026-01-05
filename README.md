# BillApp

A Windows desktop application for personal finance management, built with WPF and .NET. Designed to replace spreadsheet-based bill tracking with a secure, user-friendly local application.

## Features

### Current (Implemented)

**Bill Management**
- Create, edit, and delete bills with two-column form layout
- Track amount due, amount paid, and balance
- Due date tracking with overdue highlighting
- Categorize bills (Utilities, Housing, Insurance, etc.)
- Payment status tracking (Pending, Paid, Overdue)
- Recurring bills (Weekly, Bi-weekly, Monthly, Quarterly, Semi-annually, Annually)
- Auto-create next occurrence when a recurring bill is marked as paid
- New recurring bills carry forward reduced balance (previous balance minus payment)
- Pay dialog with payment amount, date, pay method, and confirmation number
- Track payment method (Cash or designated payment account)
- Inline editing directly in the bill list
- Filter and search bills (defaults to showing Active bills only)
- Link bills to accounts (balance syncs automatically)

**Account Management**
- Track all financial account types (Checking, Savings, Credit Card, Investment, Loan, Other)
- Store account details: name, institution, account number, balance
- Credit card specific fields: credit limit, interest rate (APR)
- Online access credentials (encrypted): login URL, username, password
- Designate accounts as payment methods (appear in pay method dropdown)
- Summary dashboard showing total assets, liabilities, and net worth
- Account balance syncs with linked bill balance
- Filter accounts by type and search by name
- Mark accounts as active/closed

**Backup & Restore**
- Manual backup with password-protected encryption (PBKDF2 + AES-256)
- Restore from backup with confirmation dialog showing backup details
- Automatic backup reminders (daily, weekly, or monthly)
- Backup retention settings (max count and age limits)
- Portable backups work on any PC (credentials re-encrypted for portability)
- Backup files stored in configurable location

**Data Import**
- Import accounts and bills from CSV files
- Pre-import validation with detailed error/warning report
- Import modes: Add to existing data or Replace all
- Smart account deduplication (by name + account number)
- Auto-create payment accounts referenced in CSV
- Flexible parsing (handles various date formats, currency symbols)
- Backup prompt before import for safety

**First-Run Setup**
- Database setup dialog on first launch
- Option to start with empty database or sample data
- Sample data includes example accounts and bills for testing

**Security**
- AES-256 encrypted database (LiteDB with password protection)
- Encryption key protected by Windows DPAPI (tied to user account)
- Automatic migration of unencrypted data on upgrade
- Field-level encryption for sensitive account credentials
- Backup encryption independent of machine (password-based)

**User Preferences**
- Window size and position remembered across sessions
- DataGrid column widths saved per view
- Backup folder and auto-backup settings
- Settings stored in `%LocalAppData%\BillApp\settings.json`

### Planned Features

| Phase | Feature | Description |
|-------|---------|-------------|
| 5 | Budget | Budget creation, spending categories, progress tracking |
| 6 | Recurring Service | Background service for auto-creating upcoming recurring bills |
| 7 | Secure Notes | Encrypted storage for passwords, PINs, and sensitive notes |
| 8 | Dashboard & Reports | Financial overview, charts, spending analysis |
| 9 | Notifications | Due date reminders and alerts |

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
│   │   ├── Services/               # Navigation, settings, etc.
│   │   ├── Settings/               # User preferences
│   │   └── Converters/             # Value converters
│   ├── BillApp.Core/               # Business Logic
│   │   ├── Models/                 # Entity classes (Bill, Account, Category)
│   │   ├── Enums/                  # Status, frequency, account type enums
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
- **Settings:** `%LocalAppData%\BillApp\settings.json`
- **Backups:** `%USERPROFILE%\Documents\BillApp Backups\` (default, configurable)

> **Note:** The database encryption key is protected by your Windows user account. If you reinstall Windows or move to a new PC, use the Backup & Restore feature to migrate your data. Backups are encrypted with a password you choose and can be restored on any machine.

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
- Sensitive account credentials (account numbers, usernames, passwords) are encrypted at field level
- Backups use PBKDF2 (100,000 iterations) key derivation with AES-256-CBC encryption
- No sensitive data is stored in plain text
- Key files, database files, and backup files are excluded from git

## License

MIT License - See [LICENSE](LICENSE) for details.

## Contributing

This is a personal project for learning WPF and desktop development. Contributions and suggestions are welcome!
