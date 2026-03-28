# Revi Flash

Revi Flash is a custom-built Anki alternative focused on clean study workflows, fast deck editing, and rich math/LaTeX support.

## What It Is

Revi Flash is a desktop flashcard app built with C# and Avalonia. It is designed for students and self-learners who want more control over study cards without relying on cloud-first tools.

*Note* - This project was heavily written using AI due to time constraints, and this may be reflected in code quality :D, will hopefully rewrite at later date.

## Key Features

- Multiple card types:
  - Flip cards
  - Type-to-answer cards
  - Multi-choice cards
  - Match-pair cards
  - True/False cards with customizable labels
- LaTeX support across card content:
  - Front and back text supports inline LaTeX segments
  - Card previews render math while editing
  - Review mode renders math content in prompts and answers
- Deck management:
  - Create, edit, delete, and search decks
  - Per-card editing with validation
- Review sessions:
  - Shuffled review order
  - Built-in correctness tracking
  - Session timer and progress indicators (configurable)
- Stats tracking:
  - Aggregate performance data
  - Per-deck and overall stats by time period
- Local-first storage:
  - SQLite-backed persistence

## Tech Stack

- C# / .NET 10
- Avalonia UI
- SQLite (via Microsoft.Data.Sqlite)
- AvaloniaMath for formula rendering

## Run The App

From the project root:

```bash
dotnet run
```

To build:

```bash
dotnet build "Revi Flash.sln"
```

## Project Goal

Revi Flash aims to be a practical, customizable, and math-friendly study tool: a custom-built Anki alternative with a modern desktop experience.
