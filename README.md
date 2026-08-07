# ReviFlash

ReviFlash is a powerful, local-first flashcard application designed as a modern desktop Anki alternative. Built with C# and Avalonia UI, it offers deep LaTeX support, flexible card types, and robust cloud synchronization capabilities.

## Overview

Designed for students, self-learners, and power users who want a fast, private, and extensible study tool. ReviFlash runs primarily offline using a local SQLite database, but features a fully integrated Cloud Manager for secure backups and community deck discovery.

## Key Features

- **Advanced Study Organization**: Group multiple flashcard decks together into Study Groups for targeted, multi-deck review sessions.
- **Comprehensive Card Types**: Support for Flip, Type-to-Answer, Multiple Choice, Match Pair, and True/False questions.
- **Native Math Rendering**: Full inline and block LaTeX rendering support within previews and active review modes.
- **Cloud Manager (Export)**: Securely upload, update, and delete your personal decks in the cloud, protected by Row-Level Security.
- **Community Hub (Import)**: Browse, search, and instantly download public flashcard sets created by other users directly into your local database.
- **In-Depth Analytics**: Track your progress with detailed statistics, grade calculations, session timing, and visual performance charts.

## Tech Stack

- C# / .NET 10
- Avalonia UI (Cross-platform Desktop Framework)
- SQLite (`Microsoft.Data.Sqlite`) for local storage
- AvaloniaMath for formula rendering
- Supabase REST API (Database & Authentication)
- AWS (Cloud Storage & Infrastructure)
- Resend (Email Delivery Services)

## Prerequisites

- .NET 10 SDK installed: https://dotnet.microsoft.com/
- A supported OS (Windows, macOS, Linux)

## Local Development

From the project root directory, restore and build the solution:

```bash
dotnet restore
dotnet build
```

Run the application locally:

```bash
dotnet run --project "ReviFlash.csproj"
```

## Automated Release Pipeline

ReviFlash uses a unified bash script to generate optimized, single-file, self-contained executables for both Linux and Windows. This means end-users do not need to install the .NET runtime to use the application.

To build the release binaries, navigate to the `BuildPipeline` directory and execute the script:

```bash
cd BuildPipeline
chmod +x build_releases.sh
./build_releases.sh
```

The compiled executables will be output to `BuildPipeline/Releases/Linux` and `BuildPipeline/Releases/Windows`.

## Contributing

Bug reports, feature requests, and pull requests are welcome. Please keep changes small and focused.

## Notes

- This project is primarily a local-first application; user data and study progress are stored locally in an SQLite database. Cloud synchronization is strictly opt-in.
- Some parts of the codebase were initially scaffolded with AI assistance — contributions to improve design, architecture, and testing coverage are highly appreciated.