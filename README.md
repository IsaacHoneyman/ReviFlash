# ReviFlash

ReviFlash is a free, open-source flashcard and lecture notes app built for exam revision in maths, science, engineering and computer science. Maths renders properly, inline with your text, from fractions and integrals to matrices and aligned working, in your cards and your notes alike. There are no due cards and no review backlog: you choose what to study and when, whether that's one topic the night before or a whole course in the weeks before your exams.

**Coming from Anki?** Import your `.apkg` decks straight in. Formatting, MathJax and `[latex]` maths, cloze deletions, type-in answers and reversed cards all come across, and sub-decks become folders.

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

## New in 1.3

**1.3.2**: Ctrl+L (Cmd+L on macOS) selects the line you're on, in notes and card fields; press it again to take in the next line too. In a note, Backspace at the start of a paragraph joins it onto the paragraph before.

**1.3.1**: bullet points. Start a line with `- ` and it shows as a bullet point, in notes and cards, with wrapped text lined up under the point. The new **• Point** button next to H1–H3, or Ctrl+P (Cmd+P on macOS), turns the selected lines into bullet points or back. Type `\-` for a dash at the start of a line that isn't a bullet point, and lists in Anki decks now import as bullet points.

**Lecture notes**
- Write your notes in ReviFlash, next to your flashcards: create one from **+ New → Note**. Notes use the same formatting, headings and LaTeX maths as cards, so the maths in your notes renders properly too.
- Each paragraph shows formatted until you click it, then you edit it as typed text. Press Esc or click elsewhere when you're done, and leave a blank line to start a new paragraph.
- Notes save themselves as you type, and when you leave the note or close ReviFlash.
- A **Contents** panel lists the note's headings; click one to jump straight to it.
- **Make Card** turns selected text (or a whole paragraph, from its right-click menu) into a Flip or Cloze card in any set. The selection becomes the answer and the nearest heading starts off as the question.
- Notes live in folders alongside your sets and groups: move, rename and delete them, find them by name in search, and show or hide them with the **Notes** toggle. They're included in backups. Notes are stored on your computer only for now.

**Stats**
- Time spent in notes is counted while a note is open and ReviFlash is the window you're using, and stops after 2 minutes without typing, clicking or scrolling.
- Overall stats show **Total Time** with **Flashcard Time** and **Notes Time** underneath.
- Every note has its own stats (the chart button on its card): time spent, last studied, words and headings.
- The graph view's study time chart shows note time stacked under flashcard time, plus two new sections: **Where Your Time Went**, your top sets and notes for the chosen period, and a **Study Calendar** of the last year, a square per day shaded by how long you studied.
- Stats update straight away after deleting something, and changing the time period in a set's or group's graph view keeps showing that set or group.

**Headings**
- `\H1{...}`, `\H2{...}` and `\H3{...}` make large, medium and small headings, in notes and in cards, with H1–H3 toolbar buttons and Ctrl+1/2/3 shortcuts (Cmd on macOS).
- Importing from Anki brings `<h1>`–`<h3>` headings, and `\section`-style headings in `[latex]` blocks, across as headings.

**Help and settings**
- A new **Help** button (the **?** next to Settings) holds the Formatting Guide and a new **Keyboard Shortcuts** list of every shortcut in the app.
- Settings are tidied: Appearance comes first (and now has the LaTeX previews option), Backup and Import from Anki share one section, and Account sits further down.

**Faster and lighter**
- ReviFlash opens about 2.5 times faster.
- The background swirl uses about a tenth of the CPU it did, pausing while ReviFlash is minimised or in the background, and is off by default for new installs (Settings → Appearance to turn it on).

**Everywhere else**
- One **+ New** menu on the main menu for sets, notes, folders and study groups.
- The deck editor opens maximized, and its formatting toolbar wraps onto a second row in a narrow window.
- Smaller windows work properly: the stats panel scrolls, buttons and toggles shorten to fit, and neither window can be made too small for its layout.
- Search boxes, sort menus and buttons in the same row are all the same height.

## Key Features

- **Built for Exams**: No due cards or review backlog. Study any deck, folder or study group whenever you like, see your grade for it, and use Retry Later to bring back the cards you got wrong.
- **Maths That Renders Properly**: Inline `$...$` and display `$$...$$` LaTeX, including fractions, sums, integrals, matrices, `cases` and aligned working, in the editor previews, reviews and notes, with a built-in Formatting Guide.
- **Lecture Notes**: Write notes with headings and LaTeX next to your flashcards, jump around them with a contents panel, and turn any part of them into a card.
- **Card Types for STEM**: Flip (optionally asked both ways), Cloze, Type-to-Answer, Multiple Choice, Match, and True/False.
- **Anki Import**: Bring in `.apkg` decks with their maths, clozes and sub-decks intact.
- **Keyboard-Driven Reviews**: Answer, mark, skip and move on without touching the mouse.
- **Folders & Study Groups**: Organise decks and notes into folders, and group decks from different topics into Study Groups for multi-deck review sessions.
- **Share Decks Online**: Upload your decks as public or private, then update, download or delete them from any device, protected by Row-Level Security. Browse and search other users' public decks and download them straight into your library.
- **Backups**: Create and restore full local backups of your library, notes and settings, with or without your stats.
- **In-Depth Analytics**: Detailed statistics, a grade for each deck, time spent on flashcards and notes, performance charts and a year-long study calendar, so you can see which topics need more work before the exam.

## Tech Stack

- C# / .NET 10
- Avalonia UI (Cross-platform Desktop Framework)
- SQLite (`Microsoft.Data.Sqlite`) for local storage
- CSharpMath for formula rendering, with Latin Modern fonts
- ZstdSharp for reading newer Anki packages
- Supabase (Database, Authentication & Storage, via its REST API)
- AWS (Cloud Infrastructure, hosting Supabase)
- Resend (Email Delivery Services)
- Velopack & GitHub Releases (For automatic updates)

## Contributing

Bug reports, feature requests, and pull requests are welcome. Please keep changes small and focused.

## Notes

- This project is primarily a local-first application; your decks, notes and study progress are stored locally in an SQLite database. Online features are strictly opt-in and need an account, and notes stay on your computer.
- Some parts of the codebase were initially scaffolded quickly during exam season — contributions to improve design, architecture, and testing coverage are highly appreciated.
