# Revi Flash

Revi Flash is a local-first, math-friendly flashcard app (a desktop Anki alternative) built with C# and Avalonia.

## Overview

Designed for students and self-learners who want a fast, private, and extensible study tool with strong LaTeX/math support and flexible card types.

## Key Features

- Multiple card types (flip, type-to-answer, multi-choice, match-pair, true/false)
- Inline and block LaTeX rendering in previews and review mode
- Deck creation, editing, searching, and per-card validation
- Shuffled review sessions, progress tracking, and basic statistics
- Local SQLite-backed storage (no cloud by default)

## Tech Stack

- C# / .NET 10
- Avalonia UI
- SQLite (Microsoft.Data.Sqlite)
- AvaloniaMath for formula rendering

## Prerequisites

- .NET 10 SDK installed: https://dotnet.microsoft.com/
- A supported OS (Windows, macOS, Linux)

## Build & Run

From the project root directory, restore and build the solution:

```bash
dotnet restore
dotnet build
```

Run the app (from the project root):

```bash
dotnet run --project "ReviFlash.csproj"
```

To create a release build / publish (example for current OS):

```bash
dotnet publish -c Release -r <RID> --self-contained false
```

Replace `<RID>` with the runtime identifier for your target platform (e.g. `linux-x64`, `win-x64`). See https://docs.microsoft.com/dotnet/core/rid-catalog for details.

## Contributing

- Bug reports, feature requests, and pull requests are welcome. Keep changes small and focused.

## Notes

- This project is a personal/local-first app; user data is stored locally in an SQLite database.
- Some parts of the codebase were initially scaffolded with AI assistance — contributions to improve design and tests are appreciated.
