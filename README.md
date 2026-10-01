# ReviFlash

ReviFlash is a powerful, open-source flashcard tool designed as a more modern desktop Anki alternative, ditching the tedious due flashcards and trusting the user. Built with C# and Avalonia, it offers LaTeX support, flexible flashcard types, folders, local backups, and online flashcard sharing.

## Download ReviFlash
Get the latest release for your platform from the [GitHub Releases Page](https://github.com/IsaacHoneyman/ReviFlash/releases/latest):

| Platform | Download Link | Notes |
| :--- | :--- | :--- |
| **Windows** | [📥 **ReviFlash-win-Setup.exe**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | Automated installer & updater |
| **Windows (portable)** | [📥 **ReviFlash-win-Portable.zip**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | No installer, unzip and run |
| **Linux** | [📥 **ReviFlash.AppImage**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | Standalone file |

> **Linux Users Note:** After downloading the `.AppImage`, make it executable before running:
> ```bash
> chmod +x ReviFlash.AppImage
> ./ReviFlash.AppImage
> ```

## Updates
Updates are handled automatically within ReviFlash! On startup the app checks whether a new version has been released and offers to update, showing the download progress before restarting into the new version. You can also check at any time from **Settings → Updates**, or turn the startup check off there.

## New in 1.1

**Accounts**
- One sign-in for all online features, which now lasts until you sign out instead of for an hour.
- The main menu shows who you're signed in as, or "Guest" with a Sign In button.
- A new Account section in Settings to sign in, sign out, and change your username.
- Forgotten passwords can be reset with a code sent by email.

**Sharing**
- Import and Export (previously Online Import and Online Export) are now download and upload icons on the main menu.
- Private decks: upload a deck as private, or switch any of your decks between public and private, to keep it out of community search. Download your own decks, public or private, from Export to move sets between your devices.
- Community decks show who uploaded them and how many times they've been downloaded. Search matches uploader names as well as titles, and the most downloaded decks come first.
- Uploads can carry their folder path, so downloads can rebuild the same folders.
- File-based import and export have been removed: use private decks to move sets, and backups to keep them safe.

**Backups and settings**
- Backups no longer contain your sign-in details, are checked before restoring, and restoring asks for confirmation first.
- Fixed restoring a backup not bringing back its settings, and restoring a backup made without stats failing in some cases.
- Deleting one deck's stats now has its own window, browsed by folder.
- The Danger Zone buttons are now readable on every theme.

**Everywhere else**
- Folders are used when picking sets in the group editor, for uploads, and when deleting deck stats.
- The ✕, ✓, + and ↔ symbols in the card editor and review screen are now proper icons.
- Typing `\$` in a text box shows a literal `$` instead of starting LaTeX.

## Key Features

- **Folders & Study Groups**: Organise decks into folders, and group multiple decks into Study Groups for targeted, multi-deck review sessions.
- **Comprehensive Card Types**: Support for Flip, Type-to-Answer, Multiple Choice, Match Pair, and True/False questions.
- **Native Math Rendering**: Full inline and block LaTeX rendering support within previews and active review modes.
- **Export**: Upload your decks to the cloud as public or private, then update, download, or delete them from any device, protected by Row-Level Security.
- **Import**: Browse and search public flashcard sets from other users, by title or uploader, and download them straight into your library.
- **Backups**: Create and restore full local backups of your library and settings, with or without your stats.
- **In-Depth Analytics**: Track your progress with detailed statistics, grade calculations, session timing, and visual performance charts.

## Tech Stack

- C# / .NET 10
- Avalonia UI (Cross-platform Desktop Framework)
- SQLite (`Microsoft.Data.Sqlite`) for local storage
- AvaloniaMath for formula rendering
- Supabase (Database, Authentication & Storage, via its REST API)
- AWS (Cloud Infrastructure, hosting Supabase)
- Resend (Email Delivery Services)
- Velopack & GitHub Releases (For automatic updates)

## Contributing

Bug reports, feature requests, and pull requests are welcome. Please keep changes small and focused.

## Notes

- This project is primarily a local-first application; your decks and study progress are stored locally in an SQLite database. Online features are strictly opt-in and need an account.
- Some parts of the codebase were initially scaffolded quickly during exam season — contributions to improve design, architecture, and testing coverage are highly appreciated.
