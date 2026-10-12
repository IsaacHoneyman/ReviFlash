using System.Diagnostics;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class ClozeUtilityTests
{
    [Fact]
    public void Groups_lists_each_number_once_in_order_of_appearance()
    {
        Assert.Equal(["2", "1"], ClozeUtility.Groups(@"\C2{a} and \C1{b} and \C2{c}"));
    }

    [Fact]
    public void Groups_gives_every_unnumbered_blank_its_own_group()
    {
        Assert.Equal(2, ClozeUtility.Groups(@"\C{a} and \C{b}").Count);
    }

    [Fact]
    public void Groups_is_empty_without_blanks()
    {
        Assert.Empty(ClozeUtility.Groups("no blanks \\B{here}"));
        Assert.False(ClozeUtility.HasBlanks("no blanks"));
    }

    [Fact]
    public void Mask_hides_the_group_and_shows_other_blanks_as_plain_text()
    {
        var text = @"\C1{a} and \C2{b}";

        Assert.Equal("[...] and b", ClozeUtility.Mask(text, "1"));
        Assert.Equal("a and [...]", ClozeUtility.Mask(text, "2"));
    }

    [Fact]
    public void Mask_hides_every_blank_sharing_a_number()
    {
        Assert.Equal("[...] and [...] and c", ClozeUtility.Mask(@"\C1{a} and \C1{b} and \C2{c}", "1"));
    }

    [Fact]
    public void Reveal_bolds_the_group_and_shows_other_blanks_as_plain_text()
    {
        Assert.Equal(@"\B{a} and b", ClozeUtility.Reveal(@"\C1{a} and \C2{b}", "1"));
    }

    [Fact]
    public void Answer_joins_the_blanks_of_a_group()
    {
        Assert.Equal("a, c", ClozeUtility.Answer(@"\C1{a} \C2{b} \C1{ c }", "1"));
    }

    [Fact]
    public void Mask_leaves_escaped_openings_alone()
    {
        Assert.Equal(@"\\C{x} [...]", ClozeUtility.Mask(@"\\C{x} \C{y}", ClozeUtility.Groups(@"\\C{x} \C{y}")[0]));
    }

    [Fact]
    public void Unclosed_blank_is_plain_text()
    {
        Assert.False(ClozeUtility.HasBlanks(@"\C{never closed"));
    }

    // --- nested blanks ---

    private const string Nested = @"\C1{a \C2{b} c}";

    [Fact]
    public void Groups_finds_nested_blanks()
    {
        Assert.Equal(["1", "2"], ClozeUtility.Groups(Nested));
    }

    [Fact]
    public void Mask_of_the_outer_group_hides_the_whole_blank()
    {
        Assert.Equal("[...]", ClozeUtility.Mask(Nested, "1"));
    }

    [Fact]
    public void Mask_of_the_inner_group_hides_only_the_inner_text()
    {
        Assert.Equal("a [...] c", ClozeUtility.Mask(Nested, "2"));
    }

    [Fact]
    public void Reveal_never_leaves_cloze_markup_visible()
    {
        Assert.Equal(@"\B{a b c}", ClozeUtility.Reveal(Nested, "1"));
        Assert.Equal(@"a \B{b} c", ClozeUtility.Reveal(Nested, "2"));
    }

    [Fact]
    public void Answer_of_a_nested_group_is_its_own_plain_text()
    {
        Assert.Equal("a b c", ClozeUtility.Answer(Nested, "1"));
        Assert.Equal("b", ClozeUtility.Answer(Nested, "2"));
    }

    [Fact]
    public void Nested_unnumbered_blanks_are_separate_groups()
    {
        var text = @"\C{a \C{b}}";

        Assert.Equal(2, ClozeUtility.Groups(text).Count);
        Assert.Equal("a [...]", ClozeUtility.Mask(text, ClozeUtility.Groups(text)[1]));
    }

    // --- empty blanks ---

    [Theory]
    [InlineData(@"\C{}")]
    [InlineData(@"\C{ }")]
    [InlineData(@"\C1{  }")]
    public void Empty_blanks_are_not_blanks(string text)
    {
        Assert.False(ClozeUtility.HasBlanks(text));
        Assert.Empty(ClozeUtility.Groups(text));
    }

    [Fact]
    public void Empty_blank_does_not_hide_anything_or_crash()
    {
        var text = @"a \C{} b \C1{x}";

        Assert.Equal(["1"], ClozeUtility.Groups(text));
        Assert.Equal("a  b [...]", ClozeUtility.Mask(text, "1"));
        Assert.Equal(@"a  b \B{x}", ClozeUtility.Reveal(text, "1"));
        Assert.Equal("x", ClozeUtility.Answer(text, "1"));
    }

    [Fact]
    public void An_empty_blank_in_a_group_is_left_out_of_its_answer()
    {
        Assert.Equal("a", ClozeUtility.Answer(@"\C1{a} \C1{ }", "1"));
    }

    // --- typed answers are plain text ---

    [Fact]
    public void Answer_unescapes_dollars_and_strips_formatting()
    {
        Assert.Equal("$5", ClozeUtility.Answer(@"\C{\$5}", ClozeUtility.Groups(@"\C{\$5}")[0]));
        Assert.Equal("x", ClozeUtility.Answer(@"\C1{\B{x}}", "1"));
        Assert.Equal("a b c d", ClozeUtility.Answer(@"\C1{\B{a \I{b}} \U{c} d}", "1"));
        Assert.Equal("one", ClozeUtility.Answer(@"\C1{\H1{one}}", "1"));
    }

    [Fact]
    public void Answer_keeps_plain_braces_and_unescapes_escaped_ones()
    {
        Assert.Equal("{a} { - \\", ClozeUtility.Answer(@"\C1{{a} \{ \- \\}", "1"));
    }

    [Fact]
    public void Answer_keeps_maths_as_source_text()
    {
        Assert.Equal(@"$x^{2}$", ClozeUtility.Answer(@"\C1{$x^{2}$}", "1"));
    }

    // --- escaping ---

    [Fact]
    public void Escaped_backslash_before_C_is_not_a_blank()
    {
        Assert.False(ClozeUtility.HasBlanks(@"\\C{x}"));
        Assert.True(ClozeUtility.HasBlanks(@"\\\C{x}"));
    }

    // --- cards ---

    [Fact]
    public void Cloze_card_makes_one_question_per_group()
    {
        var questions = new ClozeFlashCard(@"\C1{a} \C2{b}", "extra", false).CreateQuestions().ToList();

        Assert.Equal(2, questions.Count);
        Assert.Equal("[...] b", questions[0].Front);
        Assert.Equal("\\B{a} b\n\nextra", questions[0].Back);
    }

    [Theory]
    [InlineData("a, b", true)]
    [InlineData("a,b", true)]
    [InlineData("a b", true)]
    [InlineData("  A ,  B ", true)]
    [InlineData("b, a", false)]
    [InlineData("a", false)]
    [InlineData("a, c", false)]
    public void Typed_answer_with_several_blanks_accepts_loose_separators(string typed, bool expected)
    {
        var question = new ClozeFlashCard(@"\C1{a} and \C1{b}", "", true).CreateQuestions().Single();

        Assert.Equal(expected, question.VerifyAnswer(typed));
    }

    [Fact]
    public void Typed_answer_with_multi_word_blanks_compares_blank_by_blank()
    {
        var question = new ClozeFlashCard(@"\C1{new york} and \C1{paris}", "", true).CreateQuestions().Single();

        Assert.True(question.VerifyAnswer("New York,Paris"));
        Assert.True(question.VerifyAnswer("new york paris"));
        Assert.False(question.VerifyAnswer("newyork, paris"));
    }

    [Fact]
    public void Typed_answer_is_checked_against_plain_text()
    {
        var question = new ClozeFlashCard(@"cost \C{\$5} or \C2{\B{x}}", "", true).CreateQuestions().ToList();

        Assert.True(question[0].VerifyAnswer("$5"));
        Assert.True(question[1].VerifyAnswer("x"));
    }

    [Fact]
    public void Typed_answer_to_a_single_blank_is_trimmed_and_case_insensitive()
    {
        var question = new ClozeFlashCard(@"\C{Paris}", "", true).CreateQuestions().Single();

        Assert.True(question.VerifyAnswer("  paris "));
        Assert.False(question.VerifyAnswer("london"));
    }

    [Fact]
    public void Plain_type_card_still_needs_the_exact_answer()
    {
        var card = new TypeFlashCard("q", "back", "a, b");

        Assert.True(card.VerifyAnswer("A, B"));
        Assert.False(card.VerifyAnswer("a b"));
    }

    // --- speed ---

    [Fact]
    public void Unclosed_openings_are_scanned_in_linear_time()
    {
        var text = string.Concat(Enumerable.Repeat(@"\C{", 20_000));
        var clock = Stopwatch.StartNew();

        Assert.False(ClozeUtility.HasBlanks(text));
        Assert.Empty(ClozeUtility.Groups(text));
        Assert.Equal(text, ClozeUtility.Mask(text, "1"));

        Assert.True(clock.ElapsedMilliseconds < 500, $"took {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Deeply_nested_blanks_do_not_overflow_or_crawl()
    {
        const int depth = 20_000;
        var text = string.Concat(Enumerable.Repeat(@"\C{x", depth)) + new string('}', depth);
        var clock = Stopwatch.StartNew();

        Assert.Equal(depth, ClozeUtility.Groups(text).Count);
        Assert.Equal("[...]", ClozeUtility.Mask(text, ClozeUtility.Groups(text)[0]));
        _ = ClozeUtility.Reveal(text, ClozeUtility.Groups(text)[5]);
        _ = ClozeUtility.Answer(text, ClozeUtility.Groups(text)[0]);

        Assert.True(clock.ElapsedMilliseconds < 2000, $"took {clock.ElapsedMilliseconds} ms");
    }
}
