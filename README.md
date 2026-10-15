# ReviFlash

ReviFlash is a free, open-source flashcard and lecture notes app built for exam revision in maths, science, engineering and computer science. Maths renders properly, inline with your text, from fractions and integrals to matrices and aligned working, in your cards and your notes alike. There are no due cards and no review backlog: you choose what to study and when, whether that's one topic the night before or a whole course in the weeks before your exams.

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

## New in 1.4

**Images**
- Add images to cards and notes: paste one with Ctrl+V, drag an image file in, or use the new **Image** button. Each sits on its own line, and a size like `\img[50%]{...}` makes it that share of the width.
- An image inside a cloze blank stays hidden until the answer is shown.
- Images are included in backups, and ones nothing uses any more are tidied away automatically.
- Anki import now brings images across instead of marking them `[image]`.
- Sets and notes with images can't be uploaded to the cloud yet.

**Notes online**
- Upload notes just like sets: the Cloud Manager and Community windows have a **Sets / Notes** switch.
- Keep a note private to move it between your own computers, or make it public for others to find, then update, download or delete it from any device.

**Keyboard shortcuts everywhere**
- On the main menu the arrow keys move between cards, **F** or **Ctrl+F** jumps to search and **Ctrl+N** creates a set, note, folder or group.
- **E**, **M**, **S** and **Delete** edit, move, show stats for or delete the highlighted card, and **Esc** always backs out a step.
- **F1** lists every shortcut, **F2** opens Settings and **F3** the graph view. In a note, **Tab** shows the contents and **Ctrl+Enter** makes a card.

**Deck editor**
- Move or copy a card to any set; moving keeps its answer history.
- **New Card From This** starts a new card from an existing one.
- The Front and Back boxes grow as you type, the preview stays smooth on maths-heavy cards, and it only asks before discarding a card when there's something to lose.

**Stats**
- Time spent editing sets is tracked like notes time and shows as Editing Time in stats, the chart and the calendar.
- Folders have their own stats, and your best answer streak sits beside your current one.
- Study days are recorded in your own time zone, and the time periods cover exactly the days they say.

**Everywhere else**
- A new **Changelog** tab in Help, which opens after each update (you can turn that off in Settings).
- A new app icon, sharp at every size.
- A code style (Ctrl+T) for typewriter text, also inside maths.
- Spinners while online actions run, and clear messages when ReviFlash can't connect.
- Over 50 fixes, from note editing and formatting shortcuts to search speed and settings that can no longer be lost in a crash.

## Key Features

- **Built for Exams**: No due cards or review backlog. Study any deck, folder or study group whenever you like, see your grade for it, and use Retry Later to bring back the cards you got wrong.
- **Maths That Renders Properly**: Inline `$...$` and display `$$...$$` LaTeX, including fractions, sums, integrals, matrices, `cases` and aligned working, in the editor previews, reviews and notes, with a built-in Formatting Guide.
- **Lecture Notes**: Write notes with headings, LaTeX and images next to your flashcards, jump around them with a contents panel, and turn any part of them into a card.
- **Card Types for STEM**: Flip (optionally asked both ways), Cloze, Type-to-Answer, Multiple Choice, Match, and True/False.
- **Anki Import**: Bring in `.apkg` decks with their maths, images, clozes and sub-decks intact.
- **Keyboard-Driven Reviews**: Answer, mark, skip and move on without touching the mouse.
- **Folders & Study Groups**: Organise decks and notes into folders, and group decks from different topics into Study Groups for multi-deck review sessions.
- **Share Sets and Notes Online**: Upload your sets and notes as public or private, then update, download or delete them from any device, protected by Row-Level Security. Browse and search other users' public sets and notes and download them straight into your library.
- **Backups**: Create and restore full local backups of your library, notes, images and settings, with or without your stats.
- **In-Depth Analytics**: Detailed statistics, a grade for each deck, time spent on flashcards and notes, performance charts and a year-long study calendar, so you can see which topics need more work before the exam.

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

- This project is primarily a local-first application; your decks, notes and study progress are stored locally in an SQLite database. Online features are strictly opt-in and need an account; nothing is uploaded unless you upload it.
- Some parts of the codebase were initially scaffolded quickly during exam season — contributions to improve design, architecture, and testing coverage are highly appreciated.
