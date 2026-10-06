using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReviFlash.Views;

public sealed record GuideRow(string Description, string Source);

public sealed record GuideSection(string Title, string? Note, IReadOnlyList<GuideRow> Rows);

/// <summary> A shortcut: any one of <paramref name="Keys"/> does <paramref name="Action"/>. </summary>
public sealed record ShortcutRow(IReadOnlyList<string> Keys, string Action);

public sealed record ShortcutSection(string Title, string? Note, IReadOnlyList<ShortcutRow> Rows);

public enum HelpTab { Formatting, KeyboardShortcuts }

/// <summary>
/// Help: what can be typed into a card, each example shown next to how it renders, and every keyboard shortcut.
/// </summary>
public partial class SyntaxGuideWindow : Window
{
    private static SyntaxGuideWindow? _open;

    public IReadOnlyList<GuideSection> Sections { get; } =
    [
        new("Formatting", "Press Enter in a card field for a new line. Headings always sit on a line of their own.",
        [
            new("Bold", @"\B{bold text}"),
            new("Italic", @"\I{italic text}"),
            new("Underline", @"\U{underlined text}"),
            new("Bold and italic", @"\B{\I{both at once}}"),
            new("Heading", @"\H1{Big heading}"),
            new("Subheading", @"\H2{Smaller heading}"),
            new("Small heading", @"\H3{Smallest heading}"),
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
        new("Maths basics", @"These all go inside $...$ (or $$...$$).",
        [
            new("Powers and subscripts", @"$x^2$, $a_n$, $e^{i\pi}$"),
            new("Fractions", @"$\frac{a}{b}$"),
            new("Roots", @"$\sqrt{x}$, $\sqrt[3]{x}$"),
            new("Greek letters", @"$\alpha, \beta, \theta, \pi, \lambda, \Delta, \Omega$"),
            new("Sums, integrals, limits", @"$\sum_{i=1}^n i$, $\int_0^1 x\,dx$, $\lim_{x \to 0} \frac{\sin x}{x}$"),
            new("Brackets that grow", @"$\left( \frac{a}{b} \right)^2$"),
            new("Words in maths", @"$f(x) = 1 \text{ if } x > 0$"),
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
        ]),
        new("Main menu", null,
        [
            new(["Tab"], "Move between folders, sets and groups"),
            new(["Enter", "Space"], "Open the selected folder, or start reviewing the selected set or group"),
        ]),
        new("Card editor", "In a card field. Using a formatting shortcut again on text that already has it removes it.",
        [
            new(["Ctrl+B"], "Bold"),
            new(["Ctrl+I"], "Italic"),
            new(["Ctrl+U"], "Underline"),
            new(["Ctrl+1"], "Heading"),
            new(["Ctrl+2"], "Subheading"),
            new(["Ctrl+3"], "Small heading"),
            new(["Ctrl+M"], "Maths in the line"),
            new(["Ctrl+Shift+M"], "Maths on its own line"),
            new(["Ctrl+Shift+C"], "Cloze blank (Cloze cards)"),
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
        ]),
        new("Match cards", null,
        [
            new(["1–9"], "Give the selected item that answer"),
            new(["↑", "↓", "←", "→"], "Move between items"),
            new(["Backspace", "Delete"], "Undo the last answer given"),
            new(["Enter"], "Check the answers"),
        ]),
        new("Dialogs", null,
        [
            new(["Enter"], "Proceed, or Finish on the Session Complete screen"),
            new(["Esc"], "Cancel"),
        ]),
    ];

    public SyntaxGuideWindow()
    {
        InitializeComponent();
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
