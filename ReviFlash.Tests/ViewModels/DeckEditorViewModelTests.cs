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
        Assert.False(editor.HasUnsavedWork());
        editor.SelectedCardType = CARD_TYPE_MATCH;
        editor.SelectedCardType = CARD_TYPE_FLIP;

        Assert.False(editor.HasUnsavedWork());
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
        Assert.False(editor.HasUnsavedWork());

        editor.NewFront = "draft";

        Assert.True(editor.HasUnsavedWork());
    }

    [Fact]
    public void A_typed_cloze_can_not_hide_an_image_but_a_revealed_one_can()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        editor.SelectedCardType = CARD_TYPE_CLOZE;
        editor.NewFront = @"The mascot is \C{\img{0123456789abcdef0123456789abcdef.webp}}";
        editor.NewClozeTypeAnswer = true;

        editor.AddNewCard();

        Assert.Contains("can't be typed", editor.ValidationMessage);
        Assert.Empty(FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID));

        editor.NewClozeTypeAnswer = false;
        editor.AddNewCard();

        Assert.Single(FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID));
    }

    private static FlashCardDeck NewDeck(string name)
    {
        var deck = new FlashCardDeck(name);
        FlashCardRepository.SaveNewDeck(deck);
        return deck;
    }

    [Fact]
    public void Moving_a_card_takes_it_and_its_options_to_the_other_set()
    {
        using var database = new TestDatabase();
        var editor = NewEditor("From");
        var target = NewDeck("To");
        var card = new MultiFlashCard("Pick one", "", [("yes", true), ("no", false)]);
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        editor.Cards.Add(card);

        editor.MoveCard(card, target);

        Assert.Empty(editor.Cards);
        Assert.Empty(FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID));
        var moved = Assert.IsType<MultiFlashCard>(Assert.Single(FlashCardRepository.GetCardsForDeck(target.ID)));
        Assert.Equal(card.ID, moved.ID);
        Assert.Equal([("yes", true), ("no", false)], moved.Options);
    }

    [Fact]
    public void Moving_a_card_keeps_its_answer_history()
    {
        using var database = new TestDatabase();
        var editor = NewEditor("From");
        var target = NewDeck("To");
        var card = new FlipFlashCard("Q", "A");
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        Db.Execute("INSERT INTO CardResults (CardID, Part, AnsweredAt, Correct, Seconds) VALUES ($id, '', '2026-10-01 09:00:00', 1, 4);", ("$id", card.ID));

        editor.MoveCard(card, target);

        Assert.Single(FlashCardRepository.GetCardResults(card.ID));
    }

    [Fact]
    public void Moving_the_card_being_edited_clears_the_editor()
    {
        using var database = new TestDatabase();
        var editor = NewEditor("From");
        var target = NewDeck("To");
        var card = new FlipFlashCard("Q", "A");
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        editor.Cards.Add(card);
        editor.BeginEditCard(card);

        editor.MoveCard(card, target);

        Assert.False(editor.IsEditing(card));
        Assert.False(editor.HasUnsavedWork());
        Assert.Equal("Save Card", editor.SaveButtonText);
    }

    [Fact]
    public void Moving_a_card_into_its_own_set_does_nothing()
    {
        using var database = new TestDatabase();
        var editor = NewEditor("Set");
        var card = new FlipFlashCard("Q", "A");
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        editor.Cards.Add(card);

        editor.MoveCard(card, editor.CurrentDeck);

        Assert.Single(editor.Cards);
        Assert.Single(FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID));
    }

    [Fact]
    public void Clearing_a_card_being_edited_leaves_nothing_unsaved()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        var card = new FlipFlashCard("Q", "A");
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        editor.BeginEditCard(card);

        editor.NewFront = "";
        editor.NewBack = "  ";

        Assert.False(editor.HasUnsavedWork());
    }

    [Fact]
    public void An_empty_editor_has_nothing_unsaved_whatever_its_card_type()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();

        foreach (var type in editor.AvailableCardTypes)
        {
            editor.SelectedCardType = type;
            Assert.False(editor.HasUnsavedWork(), type);
        }

        editor.NewIsReversible = true;
        editor.NewFront = " ";
        Assert.False(editor.HasUnsavedWork());
    }

    [Fact]
    public void Editing_a_loaded_card_is_unsaved_work()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        var card = new FlipFlashCard("Q", "A");
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        editor.BeginEditCard(card);
        Assert.False(editor.HasUnsavedWork());

        editor.NewBack = "B";

        Assert.True(editor.HasUnsavedWork());
    }

    [Fact]
    public void Changed_true_false_labels_are_unsaved_work()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        editor.SelectedCardType = CARD_TYPE_TRUE_FALSE;

        editor.NewTrueOptionText = "Yes";

        Assert.True(editor.HasUnsavedWork());
    }

    [Fact]
    public void Copying_a_card_to_another_set_leaves_the_original_and_starts_without_history()
    {
        using var database = new TestDatabase();
        var editor = NewEditor("From");
        var target = NewDeck("To");
        var card = new MultiFlashCard("Pick one", "", [("yes", true), ("no", false)]);
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        editor.Cards.Add(card);
        Db.Execute("INSERT INTO CardResults (CardID, Part, AnsweredAt, Correct, Seconds) VALUES ($id, '', '2026-10-01 09:00:00', 1, 4);", ("$id", card.ID));

        editor.CopyCard(card, target);

        Assert.Single(editor.Cards);
        Assert.Single(FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID));
        var copy = Assert.IsType<MultiFlashCard>(Assert.Single(FlashCardRepository.GetCardsForDeck(target.ID)));
        Assert.NotEqual(card.ID, copy.ID);
        Assert.Equal([("yes", true), ("no", false)], copy.Options);
        Assert.Empty(FlashCardRepository.GetCardResults(copy.ID));
        Assert.Single(FlashCardRepository.GetCardResults(card.ID));
    }

    [Fact]
    public void Copying_a_card_into_its_own_set_adds_it_to_the_list()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        var card = new FlipFlashCard("Q", "A", isReversible: true);
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        editor.Cards.Add(card);

        editor.CopyCard(card, editor.CurrentDeck);

        Assert.Equal(2, editor.Cards.Count);
        var copy = Assert.IsType<FlipFlashCard>(editor.Cards[1]);
        Assert.Equal(("Q", "A", true), (copy.Front, copy.Back, copy.IsReversible));
        Assert.Equal(2, FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID).Count);
    }

    [Fact]
    public void Changing_the_card_type_clears_an_old_validation_message()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();

        editor.AddNewCard();
        Assert.True(editor.HasValidationMessage);

        editor.SelectedCardType = CARD_TYPE_CLOZE;

        Assert.False(editor.HasValidationMessage);
    }

    [Fact]
    public void The_editor_heading_says_edit_while_a_saved_card_is_open()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        var card = new FlipFlashCard("Q", "A");
        FlashCardRepository.SaveNewCard(card, editor.CurrentDeck.ID);
        Assert.Equal("Add New Card", editor.EditorTitle);

        editor.BeginEditCard(card);
        Assert.Equal("Edit Card", editor.EditorTitle);

        editor.AddNewCard();
        Assert.Equal("Add New Card", editor.EditorTitle);
    }

    [Fact]
    public void A_match_card_saves_and_reloads_keeping_its_left_order()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        editor.SelectedCardType = CARD_TYPE_MATCH;
        editor.MatchPairs.Clear();
        editor.MatchPairs.Add(new MatchPairEditor { LeftText = "a", RightText = "1" });
        editor.MatchPairs.Add(new MatchPairEditor { LeftText = "b", RightText = "2" });
        editor.NewMatchKeepLeftOrder = true;

        editor.AddNewCard();

        var saved = Assert.IsType<MatchFlashCard>(Assert.Single(FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID)));
        Assert.True(saved.KeepLeftOrder);
        Assert.False(editor.NewMatchKeepLeftOrder);

        editor.BeginEditCard(saved);
        Assert.True(editor.NewMatchKeepLeftOrder);

        editor.NewMatchKeepLeftOrder = false;
        Assert.True(editor.HasUnsavedWork());
    }

    [Fact]
    public void Updating_a_card_being_edited_reports_the_saved_card_but_saving_a_new_one_does_not()
    {
        using var database = new TestDatabase();
        var editor = NewEditor();
        var updated = new List<FlashCard>();
        editor.CardUpdated += updated.Add;

        editor.NewFront = "Q";
        editor.NewBack = "A";
        editor.AddNewCard();
        Assert.Empty(updated);

        var card = FlashCardRepository.GetCardsForDeck(editor.CurrentDeck.ID).Single();
        editor.BeginEditCard(card);
        editor.NewBack = "B";
        editor.AddNewCard();

        var saved = Assert.Single(updated);
        Assert.Equal((card.ID, "B"), (saved.ID, saved.Back));
    }
}
