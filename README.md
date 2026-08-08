# ReviFlash

ReviFlash is a powerful, open-source flashcard tool designed as a more modern desktop Anki alternative ditching the tedious due flashcards and trusting the user, Built with C# and Avalonia, it offers LaTex support, flexible flash card types, and robust local and online backups and flashcard sharing. 

## Download ReviFlash
Get the latest release for your platform from the [GitHub Releases Page](https://github.com/YOUR_GITHUB_USERNAME/ReviFlash/releases/latest):

| Platform | Download Link | Notes |
| :--- | :--- | :--- |
| **Windows** | [📥 **ReviFlash-win-Setup.exe**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | Automated installer & updater |
| **Linux** | [📥 **ReviFlash-Linux.AppImage**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | Standalone File |

> **Linux Users Note:** After downloading the `.AppImage`, make it executable before running:
> ```bash
> chmod +x ReviFlash.AppImage
> ./ReviFlash.AppImage
> ```
## Updates
Updates are handled automatically within ReviFlash! Upon startup the app will verify if a new version has been released, and will prompt you to update, all within the application.

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
- Github Releases (For automatic updates)

## Contributing

Bug reports, feature requests, and pull requests are welcome. Please keep changes small and focused.

## Notes

- This project is primarily a local-first application; user data and study progress are stored locally in an SQLite database. Cloud synchronization is strictly opt-in.
- Some parts of the codebase were initially scaffolded quickly during exam season — contributions to improve design, architecture, and testing coverage are highly appreciated.
