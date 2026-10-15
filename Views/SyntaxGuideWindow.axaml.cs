using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ReviFlash.Data.Local;
using ReviFlash.Utilities;

namespace ReviFlash.Views;

public sealed record GuideRow(string Description, string Source);

public sealed record GuideSection(string Title, string? Note, IReadOnlyList<GuideRow> Rows);

/// <summary> A shortcut: any one of <paramref name="Keys"/> does <paramref name="Action"/>. </summary>
public sealed record ShortcutRow(IReadOnlyList<string> Keys, string Action);

public sealed record ShortcutSection(string Title, string? Note, IReadOnlyList<ShortcutRow> Rows, IReadOnlyList<ShortcutSection>? Subsections = null);

public enum HelpTab { Formatting, KeyboardShortcuts, Changelog }

/// <summary> Help: card syntax with each example beside its rendering, every keyboard shortcut, and the changelog. </summary>
public partial class SyntaxGuideWindow : Window
{
    private static SyntaxGuideWindow? _open;

    public IReadOnlyList<ReleaseNotes> ReleaseNotes => ChangelogUtility.Bundled;

    public IReadOnlyList<GuideSection> Sections { get; } =
    [
        new("Formatting", "Press Enter in a card field for a new line. Headings and bullet points always sit on a line of their own.",
        [
            new("Bold", @"\B{bold text}"),
            new("Italic", @"\I{italic text}"),
            new("Underline", @"\U{underlined text}"),
            new("Code", @"Run \T{git status} first."),
            new("Bold and italic", @"\B{\I{both at once}}"),
            new("Heading", @"\H1{Big heading}"),
            new("Subheading", @"\H2{Smaller heading}"),
            new("Small heading", @"\H3{Smallest heading}"),
            new("Bullet points", "- First point\n- Second point, with $x^2$"),
            new("A dash at the start of a line", @"\- not a bullet point"),
            new("Maths in the line", @"The area is $\pi r^2$."),
            new("Maths on its own line", @"$$E = mc^2$$"),
            new("A dollar sign", @"It costs \$5."),
        ]),
        new("Cloze blanks", "In Cloze cards, each blank becomes its own question. Blanks with the same number are hidden together, and blanks also work inside maths.",
        [
            new("A blank", @"The capital of France is \C{Paris}."),
            new("Two questions", @"\C{Mitochondria} make \C{ATP}."),
            new("Hidden together", @"\C1{Na} and \C1{Cl} make \C2{salt}."),
            new("Inside maths", @"$\frac{d}{dx} \sin x = \C{\cos x}$"),
        ]),
        new("Images", @"Add an image with the Image button, by pasting one (Ctrl+V), or by dragging an image file in. It goes in as \img{...} on a line of its own, at its own size (smaller if it doesn't fit). A size like [50%] makes it exactly that share of the width, from 1% to 100%. An image inside a cloze blank stays hidden until the answer is shown.",
            ImageExamples()),
        new("Maths basics", @"These all go inside $...$ (or $$...$$).",
        [
            new("Powers and subscripts", @"$x^2$, $a_n$, $e^{i\pi}$"),
            new("Fractions", @"$\frac{a}{b}$"),
            new("Roots", @"$\sqrt{x}$, $\sqrt[3]{x}$"),
            new("Greek letters", @"$\alpha, \beta, \theta, \pi, \lambda, \Delta, \Omega$"),
            new("Sums, integrals, limits", @"$\sum_{i=1}^n i$, $\int_0^1 x\,dx$, $\lim_{x \to 0} \frac{\sin x}{x}$"),
            new("Brackets that grow", @"$\left( \frac{a}{b} \right)^2$"),
            new("Words in maths", @"$f(x) = 1 \text{ if } x > 0$"),
            new("Code and sans-serif", @"$\T{int main}$, $\textsf{label}$"),
            new("Vectors and sets", @"$\mathbf{v}$, $\vec{v}$, $\hat{x}$, $\mathbb{R}$, $\mathbb{Z}$"),
            new("Functions", @"$\sin x$, $\ln x$, $\log_2 x$, $\max(a, b)$"),
        ]),
        new("Symbols", null,
        [
            new("Comparisons", @"$\le, \ge, \ne, \approx, \equiv, \propto$"),
            new("Operations", @"$a \times b$, $a \div b$, $a \pm b$, $a \cdot b$, $f \circ g$"),
            new("Sets", @"$x \in A$, $x \notin A$, $A \subseteq B$, $A \cup B$, $A \cap B$, $\emptyset$"),
            new("Arrows and logic", @"$\to, \Rightarrow, \iff, \mapsto, \forall, \exists, \neg$"),
            new("Calculus", @"$\infty, \partial, \nabla, \dot{x}, f'(x)$"),
        ]),
        new("Layouts", null,
        [
            new("Matrix", @"$\begin{pmatrix} 1 & 2 \\ 3 & 4 \end{pmatrix}$"),
            new("Square brackets", @"$\begin{bmatrix} a & b \\ c & d \end{bmatrix}$"),
            new("Determinant", @"$\begin{vmatrix} a & b \\ c & d \end{vmatrix} = ad - bc$"),
            new("Cases", @"$|x| = \begin{cases} x & x \ge 0 \\ -x & x < 0 \end{cases}$"),
            new("Aligned working", @"$$\begin{aligned} (a+b)^2 &= (a+b)(a+b) \\ &= a^2 + 2ab + b^2 \end{aligned}$$"),
        ]),
    ];

    public IReadOnlyList<ShortcutSection> ShortcutSections { get; } =
    [
        new("Anywhere", null,
        [
            new(["F11"], "Switch between fullscreen and a window"),
            new(["F1"], "Show this list of shortcuts"),
            new(["Esc"], "Go back a step: close the window, stop editing, or leave the page"),
            new(["Enter"], "Proceed in a dialog, or Finish on the Session Complete screen"),
        ]),
        new("Main menu", "Card keys act on the card with the highlight; use the arrows to move it.",
        [
            new(["F", "Ctrl+F"], "Search"),
            new(["←", "↑", "→", "↓"], "Move between cards"),
            new(["Home", "End"], "Go to the first or last card"),
            new(["Enter", "Space"], "Open a folder or note, or start reviewing a set or group"),
            new(["E"], "Edit a set or group, or rename a folder or note"),
            new(["M"], "Move the card to another folder"),
            new(["S"], "Show the card's stats"),
            new(["Delete"], "Delete the card (asks first)"),
            new(["Ctrl+N"], "Create a set, note, folder or group"),
            new(["F2"], "Open Settings"),
            new(["F3"], "Open the graph view"),
            new(["Esc"], "Clear the search, close stats or the graph, stop picking sets, then leave the folder; on the main menu, quit"),
        ]),
        new("Formatting", "In card fields and note paragraphs. Using a formatting shortcut again on text that already has it removes it.",
        [
            new(["Ctrl+B"], "Bold"),
            new(["Ctrl+I"], "Italic"),
            new(["Ctrl+U"], "Underline"),
            new(["Ctrl+T"], "Code (a typewriter font)"),
            new(["Ctrl+1"], "Heading"),
            new(["Ctrl+2"], "Subheading"),
            new(["Ctrl+3"], "Small heading"),
            new(["Ctrl+P"], "Bullet points (on every line selected)"),
            new(["Ctrl+L"], "Select the line (again for the next line too)"),
            new(["Ctrl+M"], "Maths in the line"),
            new(["Ctrl+Shift+M"], "Maths on its own line"),
            new(["Ctrl+Shift+C"], "Cloze blank (Cloze cards)"),
            new(["Ctrl+V"], "Paste an image, or text as usual"),
        ]),
        new("Notes", "Click a paragraph to edit it.",
        [
            new(["Esc"], "Finish editing the paragraph, or go back to the library when not editing"),
            new(["Tab"], "Show or hide the contents"),
            new(["Ctrl+Enter"], "Make a card from the selected text or the paragraph being edited, then again to add it"),
            new(["↑"], "On its first line: edit the paragraph before"),
            new(["↓"], "On its last line: edit the paragraph after"),
            new(["Enter ×2"], "A blank line starts a new paragraph"),
            new(["Backspace"], "At its start: join it onto the paragraph before"),
        ]),
        new("Reviews", "A hint under each card shows the keys that work right now. While typing an answer, only Enter and Esc work.",
        [
            new(["Space", "Enter"], "Show the answer on a Flip card"),
            new(["←", "1"], "Mark incorrect, or answer False"),
            new(["→", "2"], "Mark correct, or answer True"),
            new(["1–9"], "Tick a multiple choice option"),
            new(["Enter"], "Check a typed or multiple choice answer"),
            new(["Enter", "Space", "→"], "Go to the next card once answered"),
            new(["R"], "Retry the card later"),
            new(["S"], "Skip the card"),
            new(["Esc"], "Quit the review"),
        ],
        [
            new("Match cards", null,
            [
                new(["1–9"], "Give the selected item that answer"),
                new(["↑", "↓", "←", "→"], "Move between items"),
                new(["Backspace", "Delete"], "Undo the last answer given"),
                new(["Enter"], "Check the answers"),
            ]),
        ]),
    ];

    /// <summary> Examples using the ReviFlash logo, stored like any added image; none if it can't be stored. </summary>
    private static List<GuideRow> ImageExamples()
    {
        try
        {
            using var logo = AssetLoader.Open(new Uri("avares://ReviFlash/Assets/ReviFlashLogo.ico"));
            using var bytes = new MemoryStream();
            logo.CopyTo(bytes);
            var name = ImageStore.Add(bytes.ToArray());
            return [new("An image", ImageToken.Format(name)), new("A quarter of the width", ImageToken.Format(name, 25))];
        }
        catch (Exception ex)
        {
            Logger.LogError("Couldn't store the example image for the formatting guide.", ex);
            return [];
        }
    }

    public SyntaxGuideWindow()
    {
        InitializeComponent();
        Shortcuts.CloseOnEscape(this);
        DataContext = this;
    }

    /// <summary> Opens help on <paramref name="tab"/> beside <paramref name="owner"/>, or brings the open one to the front. </summary>
    public static void ShowFor(Window owner, HelpTab tab = HelpTab.Formatting)
    {
        if (_open is not null)
        {
            _open.Tabs.SelectedIndex = (int)tab;
            _open.Activate();
            return;
        }

        _open = new SyntaxGuideWindow();
        _open.Tabs.SelectedIndex = (int)tab;
        _open.Closed += (_, _) => _open = null;
        _open.Show(owner);
    }

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not GuideRow row || Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(row.Source);
    }
}
