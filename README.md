# ReviFlash

**ReviFlash doesn't think for you.** It's a free, open-source flashcard and lecture notes app for exam revision in maths, science, engineering and computer science. You write your own cards, because writing them is half the revision, and you choose what to study and when. There are no due cards, no backlog and no AI deciding for you, just the run-up to your exam on your terms.

Maths renders properly, inline with your text: fractions, integrals, matrices and aligned working, in your cards and your notes alike.

![The ReviFlash main menu](docs/images/main-menu.png)

| Maths that renders properly | Lecture notes next to your cards |
| :---: | :---: |
| ![A review card with aligned working](docs/images/review-maths.png) | ![A lecture note with a contents panel](docs/images/lecture-notes.png) |
| **Stats for every set, folder and note** | **Themes, or make your own** |
| ![Charts of answers and study time](docs/images/stats.png) | ![The Themes window](docs/images/themes.png) |

**Coming from Anki?** Import your `.apkg` decks straight in. Formatting, MathJax and `[latex]` maths, images, cloze deletions, type-in answers and reversed cards all come across, and sub-decks become folders.

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

## New in 1.4.1

A clean-up release: everything looks and works the same way in every window, and themes are now yours to make.

**Themes**
- A new **Themes** window, opened from Settings, shows every theme as a tile drawn in its own colours, listed round the colour wheel.
- **Make your own:** pick a background and two accents and ReviFlash works out the rest, keeping text and badges readable. Change any single colour if you want, name it, and keep up to 12. They're saved in your backups.
- Seventeen themes were retired, including the light themes. If you were using one, ReviFlash switches to Vaporwave.

**A cleaner main menu**
- Every card has the same kind of line under its name: when a set was last studied, a group's cards, a folder's contents, or a note's last edit.
- Notes have their own colour, and their badge shows the word count.
- Badges are readable on every theme and say "1 Card", "1 Set" and "1 Item".

**New installs**
- ReviFlash starts with a **Welcome!** folder: a Feature Tour with one of every card type, an example set and study group, and a Read Me on how to use the app.

**Clearer messages**
- When something goes wrong, like an upload, backup or update check, a message pops up with the whole explanation instead of a cut-off line in the corner.
- Confirmations and errors have a short title, like "Delete Set?", with the details underneath.
- Success messages show briefly and then fade.

**Everywhere else**
- Every button, checkbox and toggle shows a clear highlight when you reach it with Tab or the arrow keys.
- One consistent set of text sizes, colours and capitalisation across every window, and the app says "set" everywhere.
- Community and the Cloud Manager switch between sets and notes with a sliding switch.
- Delete All Stats now clears notes time and your launch streak too.
- Typed answers ignore extra spaces, and grades always match the percentage shown.
- Images and display maths no longer get extra blank lines around them.
- Over 20 more fixes, from stale Community results to deleted folders lingering in stats.

## Key Features

- **Built for Exams**: No due cards or review backlog. Study any set, folder or study group whenever you like, see your grade for it, and use Retry Later to bring back the cards you got wrong.
- **Maths That Renders Properly**: Inline `$...$` and display `$$...$$` LaTeX, including fractions, sums, integrals, matrices, `cases` and aligned working, in the editor previews, reviews and notes, with a built-in Formatting Guide.
- **Lecture Notes**: Write notes with headings, LaTeX and images next to your flashcards, jump around them with a contents panel, and turn any part of them into a card.
- **Card Types for STEM**: Flip (optionally asked both ways), Cloze, Type-to-Answer, Multiple Choice, Match, and True/False.
- **Anki Import**: Bring in `.apkg` decks with their maths, images, clozes and sub-decks intact.
- **Themes**: Pick from 18 built-in themes or make your own from three colours, with readable text guaranteed.
- **Keyboard-Driven Reviews**: Answer, mark, skip and move on without touching the mouse.
- **Folders & Study Groups**: Organise sets and notes into folders, and group sets from different topics into Study Groups to review them together.
- **Share Sets and Notes Online**: Upload your sets and notes as public or private, then update, download or delete them from any device, protected by Row-Level Security. Browse and search other users' public sets and notes and download them straight into your library.
- **Backups**: Create and restore full local backups of your library, notes, images and settings, with or without your stats.
- **In-Depth Analytics**: Detailed statistics, a grade for each set, time spent on flashcards and notes, performance charts and a year-long study calendar, so you can see which topics need more work before the exam.

## Tech Stack

- C# / .NET 10
- Avalonia UI (Cross-platform Desktop Framework)
- SQLite (`Microsoft.Data.Sqlite`) for local storage
- CSharpMath for formula rendering, with Latin Modern fonts
- SkiaSharp for images (stored as WebP)
- ZstdSharp for reading newer Anki packages
- Supabase (Database, Authentication & Storage, via its REST API)
- AWS (Cloud Infrastructure, hosting Supabase)
- Resend (Email Delivery Services)
- Velopack & GitHub Releases (For automatic updates)

## Contributing

Bug reports, feature requests, and pull requests are welcome. Please keep changes small and focused.

## Notes

- This project is primarily a local-first application; your sets, notes and study progress are stored locally in an SQLite database. Online features are strictly opt-in and need an account; nothing is uploaded unless you upload it.
- The app has over 480 unit tests, which run on Windows, macOS and Linux. Contributions are welcome.
