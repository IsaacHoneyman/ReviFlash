# ReviFlash

ReviFlash is a free, open-source flashcard app for exam revision, built for maths and science. There are no due cards and no review backlog: you choose what to study and when, whether that's one topic the night before or a whole course in the weeks before your exams. Maths renders properly, inline with your text, and cards can be flip, type-the-answer, multiple choice, match-the-pairs or true/false.

Spaced-repetition apps like Anki are built for remembering things over months. ReviFlash is built for the run-up to an exam: drill a topic now, see your grade for it, and come back to the cards you got wrong.

## Download ReviFlash
Get the latest release for your platform from the [GitHub Releases Page](https://github.com/IsaacHoneyman/ReviFlash/releases/latest):

| Platform | Download Link | Notes |
| :--- | :--- | :--- |
| **Windows** | [📥 **ReviFlash-win-Setup.exe**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | Automated installer & updater |
| **Windows (portable)** | [📥 **ReviFlash-win-Portable.zip**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | No installer, unzip and run |
| **macOS (Apple Silicon)** | [📥 **ReviFlash-osx-Setup.pkg**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | Installer & updater, see the note below |
| **macOS (portable)** | [📥 **ReviFlash-osx-Portable.zip**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | No installer, unzip and run |
| **Linux** | [📥 **ReviFlash.AppImage**](https://github.com/IsaacHoneyman/ReviFlash/releases/latest) | Standalone file |

> **Linux Users Note:** After downloading the `.AppImage`, make it executable before running:
> ```bash
> chmod +x ReviFlash.AppImage
> ./ReviFlash.AppImage
> ```

> **macOS Users Note:** ReviFlash isn't signed with a paid Apple Developer account, so the first time you open it macOS will say it can't be verified. Close that message, then go to **System Settings → Privacy & Security**, scroll down and click **Open Anyway**. You only need to do this once. If macOS instead says the app is damaged, run this in Terminal and open it again:
> ```bash
> xattr -dr com.apple.quarantine /Applications/ReviFlash.app
> ```

## Updates
Updates are handled automatically within ReviFlash! On startup the app checks whether a new version has been released and offers to update, showing the download progress before restarting into the new version. You can also check at any time from **Settings → Updates**, or turn the startup check off there.

## New in 1.1.1

**macOS**
- ReviFlash now runs on Macs with Apple Silicon (M1 and later), with the same automatic updates as Windows and Linux.

**LaTeX**
- Maths now flows inline with the text around it and wraps with it, instead of jumping onto its own line.
- Maths looks the same in the card editor previews and the review screen, at the same size as the text around it.
- `$$...$$` shows maths centred on its own line, like in LaTeX, while `$...$` stays inline.
- Many more LaTeX commands work, including `\mathbb`, `\mathbf`, `\operatorname`, `\mapsto`, `\iff`, `\implies`, `\notin`, `\quad`, `\dots` and `\overline`.
- New matrix and layout environments: `bmatrix`, `vmatrix`, `Bmatrix`, `Vmatrix`, `cases`, `aligned` and `array`, alongside `pmatrix` and `matrix`.
- Bold and italic text with `\B{...}` and `\I{...}`, which can be combined and also work inside maths.
- Cards with LaTeX that can't be shown now show the text as typed in reviews, and the error in the editor preview.

**Settings**
- New **LaTeX Font for Card Text** option under Appearance shows card text in the LaTeX font, to match the maths (off by default).

## Key Features

- **Folders & Study Groups**: Organise decks into folders, and group multiple decks into Study Groups for targeted, multi-deck review sessions.
- **Comprehensive Card Types**: Support for Flip, Type-to-Answer, Multiple Choice, Match Pair, and True/False questions.
- **Revise on Your Terms**: No due cards or review backlog. Study any deck, folder or study group whenever you like, and use Retry Later to bring back the cards you got wrong.
- **Maths That Renders Properly**: Inline `$...$` and display `$$...$$` LaTeX, including fractions, sums, integrals, matrices and `cases`, in the editor previews and while reviewing. Use `\B{...}` and `\I{...}` for bold and italic text.
- **Export**: Upload your decks to the cloud as public or private, then update, download, or delete them from any device, protected by Row-Level Security.
- **Import**: Browse and search public flashcard sets from other users, by title or uploader, and download them straight into your library.
- **Backups**: Create and restore full local backups of your library and settings, with or without your stats.
- **In-Depth Analytics**: Track your progress with detailed statistics, a grade for each deck, session timing, and visual performance charts, so you can see which topics need more work before the exam.

## Tech Stack

- C# / .NET 10
- Avalonia UI (Cross-platform Desktop Framework)
- SQLite (`Microsoft.Data.Sqlite`) for local storage
- CSharpMath for formula rendering
- Supabase (Database, Authentication & Storage, via its REST API)
- AWS (Cloud Infrastructure, hosting Supabase)
- Resend (Email Delivery Services)
- Velopack & GitHub Releases (For automatic updates)

## Contributing

Bug reports, feature requests, and pull requests are welcome. Please keep changes small and focused.

## Notes

- This project is primarily a local-first application; your decks and study progress are stored locally in an SQLite database. Online features are strictly opt-in and need an account.
- Some parts of the codebase were initially scaffolded quickly during exam season — contributions to improve design, architecture, and testing coverage are highly appreciated.
