# ReviFlash

ReviFlash is a free, open-source flashcard app built for exam revision in maths, science, engineering and computer science. Maths renders properly, inline with your text, from fractions and integrals to matrices and aligned working. There are no due cards and no review backlog: you choose what to study and when, whether that's one topic the night before or a whole course in the weeks before your exams.

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

## New in 1.2

**Import from Anki**
- Bring your Anki decks across from **Settings → Import from Anki**. It works with `.apkg` files from any Anki version, including the newer compressed format.
- Formatting, MathJax and `[latex]` maths, cloze deletions (including inside maths), "type in the answer" cards and reversed cards all come across, and sub-decks become folders.
- Images aren't supported yet: cards with images keep their text and are marked `[image]`, and the import tells you which decks they're in.

**New card options**
- **Cloze** cards: wrap the parts to hide in `\C{...}` and each blank becomes its own question. Blanks with the same number (`\C1{...}`) are hidden together, blanks work inside maths, and you can reveal the answer or type it.
- Flip cards can be **asked both ways**: tick "Also ask in reverse" and reviews ask back to front as well.
- Underlined text with `\U{...}`, alongside `\B{...}` and `\I{...}`.

**Card editor**
- A formatting toolbar above the card fields: Bold, Italic, Underline, Maths, Maths Block and (on Cloze cards) Blank, with keyboard shortcuts (Ctrl+B, Ctrl+I, Ctrl+U, Ctrl+M, Ctrl+Shift+M, Ctrl+Shift+C; Cmd on macOS).
- A new **Formatting Guide** (the **?** button, or Settings → Editor) with examples of formatting, cloze blanks and LaTeX next to how they look, each with a copy button.

**Reviews**
- Fully keyboard driven: Space shows the answer, ← / → mark it, number keys pick options, Enter submits and moves on, S skips, R retries later and Esc quits. A hint under the card shows the keys that work right now.
- Match cards are answered by picking answer chips (click them or press their number) instead of drop-down menus, and show the right answer under any pair you got wrong.
- A wider card with bigger text: short questions stay centred, longer ones are left-aligned, and maths is sized to match the text around it.
- One consistent button style, True/False buttons that don't hint at the answer, Retry Later on flip cards, and the card type (and deck, in study groups) shown at the top.
- Skipping the last card ends the review, and Enter finishes from the results screen.

**Everywhere else**
- Deleting a folder can also delete everything inside it, with a confirmation saying exactly how much will go.
- The LaTeX font option now uses Latin Modern for card text, with bold, italic and underline all working.
- Confirmation dialogs: Enter proceeds and Esc cancels.
- Fixed a crash from maths that can't be laid out, and changing a Type to Answer card to another type.

## Key Features

- **Built for Exams**: No due cards or review backlog. Study any deck, folder or study group whenever you like, see your grade for it, and use Retry Later to bring back the cards you got wrong.
- **Maths That Renders Properly**: Inline `$...$` and display `$$...$$` LaTeX, including fractions, sums, integrals, matrices, `cases` and aligned working, in the editor previews and while reviewing, with a built-in Formatting Guide.
- **Card Types for STEM**: Flip (optionally asked both ways), Cloze, Type-to-Answer, Multiple Choice, Match, and True/False.
- **Anki Import**: Bring in `.apkg` decks with their maths, clozes and sub-decks intact.
- **Keyboard-Driven Reviews**: Answer, mark, skip and move on without touching the mouse.
- **Folders & Study Groups**: Organise decks into folders, and group decks from different topics into Study Groups for multi-deck review sessions.
- **Share Decks Online**: Upload your decks as public or private, then update, download or delete them from any device, protected by Row-Level Security. Browse and search other users' public decks and download them straight into your library.
- **Backups**: Create and restore full local backups of your library and settings, with or without your stats.
- **In-Depth Analytics**: Detailed statistics, a grade for each deck, session timing and performance charts, so you can see which topics need more work before the exam.

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

- This project is primarily a local-first application; your decks and study progress are stored locally in an SQLite database. Online features are strictly opt-in and need an account.
- Some parts of the codebase were initially scaffolded quickly during exam season — contributions to improve design, architecture, and testing coverage are highly appreciated.
