using Avalonia.Input;
using ReviFlash.Utilities;
using ReviFlash.Views;

namespace ReviFlash.Tests.Utilities;

public class CodeFormattingTests
{
    [Fact]
    public void T_marks_its_text_as_code()
    {
        var segments = LatexUtility.Parse(@"Run \T{git status} now").OfType<TextSegment>().ToList();

        Assert.Equal(["Run ", "git status", " now"], segments.Select(s => s.Text));
        Assert.Equal([false, true, false], segments.Select(s => s.Code));
    }

    [Fact]
    public void Code_nests_with_the_other_styles()
    {
        var segment = LatexUtility.Parse(@"\B{\T{x}}").OfType<TextSegment>().Single();

        Assert.True(segment.Bold);
        Assert.True(segment.Code);
    }

    [Fact]
    public void A_longer_command_starting_with_T_is_not_code()
    {
        Assert.DoesNotContain(LatexUtility.Parse(@"\Tx{a}").OfType<TextSegment>(), s => s.Code);
    }

    [Theory]
    [InlineData(@"\T{int main}", @"\mathtt{int\ main}")]
    [InlineData(@"\Theta + \T{x}", @"\Theta + \mathtt{x}")]
    public void In_maths_T_is_typewriter_with_its_spaces_kept(string input, string expected)
    {
        Assert.Equal(expected, LatexUtility.ToMathLatex(input));
    }

    [Fact]
    public void Ctrl_T_wraps_the_selection_in_T()
    {
        var e = new KeyEventArgs { Key = Key.T, KeyModifiers = KeyModifiers.Control };

        Assert.Equal((@"\T{", "}"), TextFormatting.ShortcutFor(e, null, allowCloze: false));
    }

    [Theory]
    [InlineData("<code>ls -a</code>", @"\T{ls -a}")]
    [InlineData("<tt>x</tt>", @"\T{x}")]
    [InlineData("<kbd>Ctrl</kbd>", @"\T{Ctrl}")]
    [InlineData(@"[latex]Run \texttt{make}[/latex]", @"Run \T{make}")]
    public void Anki_code_comes_in_as_T(string field, string expected)
    {
        Assert.Equal(expected, AnkiTextConverter.Convert(field));
    }

    [Fact]
    public void An_Anki_backslash_T_in_plain_text_stays_text()
    {
        var converted = AnkiTextConverter.Convert(@"C:\Temp{1}");

        Assert.DoesNotContain(LatexUtility.Parse(converted).OfType<TextSegment>(), s => s.Code);
    }
}
