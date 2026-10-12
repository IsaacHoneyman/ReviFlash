using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using static ReviFlash.Utilities.CardUtility;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class DeckEditorViewModelTests
{
    private static DeckEditorViewModel NewEditor(string name = "Set")
    {
        var deck = new FlashCardDeck(name);
        FlashCardRepository.SaveNewDeck(deck);
        return new DeckEditorViewModel(deck);
    }

    private static string SavedName(DeckEditorViewModel editor) =>
        FlashCardRepository.GetAllDecks().Single(d => d.ID == editor.CurrentDeck.ID).Name;

    [Fact]
    public void A_blank_set_name_is_never_saved()
    {
        using var database = new TestDatabase();
        var editor = NewEditor("Biology");

        editor.DeckName = "";
        editor.DeckName = "   ";

        Assert.Equal("Biology", SavedName(editor));
    }

    [Fact]
    public void Committing_a_blank_set_name_restores_the_last_good_name()
    {
        using var database = new TestDatabase();
        var editor = NewEditor("Biology");

        editor.DeckName = "Chemistry";
        editor.DeckName = "";
        editor.CommitDeckName();

        Assert.Equal("Chemistry", editor.DeckName);
        Assert.Equal("Chemistry", SavedName(editor));
    }

    [Fact]
    public void Switching_away_from_match_clears_the_placeholder_text()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();

        editor.SelectedCardType = CARD_TYPE_MATCH;
        Assert.Equal(CARD_TYPE_MATCH_PLACEHOLDER, editor.NewFront);

        editor.SelectedCardType = CARD_TYPE_FLIP;

        Assert.Equal("", editor.NewFront);
        Assert.Equal("", editor.NewBack);
    }

    [Fact]
    public void Switching_away_from_match_keeps_text_the_user_typed()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();

        editor.SelectedCardType = CARD_TYPE_MATCH;
        editor.NewFront = "Capitals";
        editor.SelectedCardType = CARD_TYPE_FLIP;

        Assert.Equal("Capitals", editor.NewFront);
        Assert.Equal("", editor.NewBack);
    }

    [Fact]
    public void A_match_editor_with_only_the_placeholder_counts_as_blank()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();

        editor.SelectedCardType = CARD_TYPE_FLIP;
        Assert.True(editor.EditorIsBlank());
        editor.SelectedCardType = CARD_TYPE_MATCH;
        editor.SelectedCardType = CARD_TYPE_FLIP;

        Assert.True(editor.EditorIsBlank());
    }

    [Fact]
    public void Saving_trims_blank_lines_around_front_and_back_but_not_inside()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();

        editor.NewFront = "\r\n\nQuestion  one\n\nline two\n\n";
        editor.NewBack = "\nAnswer \r\n";
        editor.AddNewCard();

        var card = Assert.Single(FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID));
        Assert.Equal("Question  one\n\nline two", card.Front);
        Assert.Equal("Answer ", card.Back);
    }

    [Fact]
    public void An_unsaved_card_means_the_editor_is_not_blank()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        Assert.True(editor.EditorIsBlank());

        editor.NewFront = "draft";

        Assert.False(editor.EditorIsBlank());
    }
}
