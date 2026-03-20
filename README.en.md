# FastCli

[中文版本](./README.md)

FastCli is a Windows command workspace built with `C# / .NET 8 / WPF / SQLite`, designed to organize and run local CLI commands from a single desktop application.

It is useful for turning frequently used scripts, development commands, operations commands, and interactive CLI tools into reusable command entries instead of repeatedly switching terminals and copying commands by hand.

## Features

- Group management: create, rename, delete, and drag to reorder
- Command management: create, duplicate, delete, edit, and reorder within a group
- Cross-group move: drag a command directly into another group
- Multiple shell types: `cmd`, `Windows PowerShell`, `pwsh`, and direct program launch
- Two execution modes:
  - `Embedded`: run inside the app with live output
  - `External Terminal`: better for interactive commands
- Environment variables: define per-command environment variables
- Execution history: store recent runs, summaries, and terminal output
- Terminal log improvements:
  - automatic system hints when execution starts or ends
  - different colors for system hints and error output
  - copy and clear current log
- Local persistence with `SQLite`
- Light and dark theme support

## Use Cases

- Local development command launcher
- Startup command organizer for frontend/backend projects
- Operations scripts and batch command management
- AI / CLI tool command collection
- Repeated local tooling workflows

## Tech Stack

- `C#`
- `.NET 8`
- `WPF`
- `SQLite`

## Requirements

- Windows 10 / 11
- `.NET 8 SDK`

## Quick Start

### 1. Clone the repository

```powershell
git clone https://github.com/mostbean-cn/fast-cli.git
cd fast-cli
```

### 2. Run with Visual Studio or the .NET CLI

```powershell
dotnet run --project .\FastCli.Desktop\FastCli.Desktop.csproj
```

You can also open [FastCli.sln](./FastCli.sln) directly in Visual Studio.

## Data Storage

- On startup, the app loads the embedded [sql/001_init.sql](./sql/001_init.sql) script to initialize the local database
- Default local database path:
  - `%LOCALAPPDATA%\FastCli\fastcli.db`
- Selection state path:
  - `%LOCALAPPDATA%\FastCli\selection-state.json`

## Project Structure

- `FastCli.Desktop`: WPF UI, interaction logic, themes, and ViewModels
- `FastCli.Application`: application services and execution orchestration
- `FastCli.Domain`: domain models and enums
- `FastCli.Infrastructure`: SQLite persistence and command execution implementations
- `sql`: database initialization script
- `docs`: project documentation
- `assets`: icons and static assets

## Current Limitations

- `Embedded execution + administrator privileges` is not supported at the same time
- Interactive commands are better suited for `External Terminal` mode
- This is currently a Windows desktop application only

## Development Notes

- The project follows a layered architecture:
  - `Desktop -> Application -> Domain`
  - `Infrastructure` provides external capability implementations
- Execution summaries and output are persisted for history browsing
- All command data is stored locally without a backend service

## Related Docs

- Version notes: [docs/fastcli-v1.md](./docs/fastcli-v1.md)

## License

No license file is currently included. If you plan to publish this project publicly, adding a `LICENSE` file is recommended.
