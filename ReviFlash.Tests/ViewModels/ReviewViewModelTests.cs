using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using static ReviFlash.Utilities.CardUtility;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class ReviewViewModelTests
{
    private static FlashCardDeck NewDeck(string name, int cardCount)
    {
        var deck = new FlashCardDeck(name);
        FlashCardRepository.SaveNewDeck(deck);
        for (var i = 0; i < cardCount; i++)
        {
            var card = new FlipFlashCard($"{name} front {i}", $"{name} back {i}");
            FlashCardRepository.SaveNewCard(card, deck.ID);
            deck.AddFlashCard(card);
        }
        return deck;
    }

    private static ReviewViewModel NewReview(params FlashCardDeck[] decks)
    {
        MetaDataManager.Data!.ShowSkipButton = true;
        MetaDataManager.Data.ShowRetryLaterButton = true;
        var map = decks.SelectMany(deck => deck.FlashCards.Select(card => (cardId: card.ID, deckId: deck.ID))).ToDictionary(pair => pair.cardId, pair => pair.deckId);
        return new ReviewViewModel(decks.SelectMany(deck => deck.FlashCards), decks[0].ID, decks.Length > 1 ? map : null);
    }

    [Fact]
    public void Best_streaks_are_recorded_per_deck_when_several_decks_are_reviewed_without_a_group()
    {
        using var database = new TestDatabase();
        var first = NewDeck("First", 2);
        var second = NewDeck("Second", 1);
        var review = NewReview(first, second);

        for (var i = 0; i < 3; i++) review.MarkCorrect();

        Assert.Equal(2, FlashCardRepository.GetBestAnswerStreak("Deck", first.ID));
        Assert.Equal(1, FlashCardRepository.GetBestAnswerStreak("Deck", second.ID));
    }

    [Fact]
    public void The_session_streak_still_spans_every_deck()
    {
        using var database = new TestDatabase();
        var review = NewReview(NewDeck("First", 2), NewDeck("Second", 1));

        review.MarkCorrect();
        review.MarkCorrect();

        Assert.Equal(2, review.CurrentAnswerStreak);
    }

    [Fact]
    public void Study_time_is_recorded_when_nothing_was_scored()
    {
        using var database = new TestDatabase();
        var deck = NewDeck("Deck", 2);
        var review = NewReview(deck);

        review.Reveal();
        Thread.Sleep(1200);
        review.QuitSession();

        var (_, total, seconds) = FlashCardRepository.GetStats(deck.ID);
        Assert.Equal(0, total);
        Assert.True(seconds >= 1);
    }

    [Fact]
    public void Unscored_study_time_is_split_across_the_decks_in_the_session()
    {
        using var database = new TestDatabase();
        var first = NewDeck("First", 1);
        var second = NewDeck("Second", 1);
        var review = NewReview(first, second);

        Thread.Sleep(2200);
        review.QuitSession();

        Assert.True(FlashCardRepository.GetStats(first.ID).timeTakenSeconds >= 1);
        Assert.True(FlashCardRepository.GetStats(second.ID).timeTakenSeconds >= 1);
    }

    [Fact]
    public void Retry_later_is_unavailable_with_one_card_left()
    {
        using var database = new TestDatabase();
        var review = NewReview(NewDeck("Deck", 1));

        review.Reveal();

        Assert.False(review.CanRetryLater);
        Assert.DoesNotContain("retry later", review.KeyboardHint);
    }

    [Fact]
    public void Retry_later_is_available_once_a_flip_card_is_revealed_with_more_cards_left()
    {
        using var database = new TestDatabase();
        var review = NewReview(NewDeck("Deck", 2));

        Assert.False(review.CanRetryLater);
        review.Reveal();

        Assert.True(review.CanRetryLater);
    }

    [Fact]
    public void Skip_is_only_available_before_a_flip_card_is_revealed()
    {
        using var database = new TestDatabase();
        var review = NewReview(NewDeck("Deck", 2));

        Assert.True(review.CanSkip);
        review.Reveal();

        Assert.False(review.CanSkip);
        Assert.DoesNotContain("skip", review.KeyboardHint);
    }

    [Fact]
    public void Skip_is_not_available_after_a_card_is_checked()
    {
        using var database = new TestDatabase();
        var deck = new FlashCardDeck("Deck");
        FlashCardRepository.SaveNewDeck(deck);
        foreach (var card in new[] { new TrueFalseFlashCard("Sky is blue", "", true), new TrueFalseFlashCard("Grass is red", "", false) })
        {
            FlashCardRepository.SaveNewCard(card, deck.ID);
            deck.AddFlashCard(card);
        }
        var review = NewReview(deck);

        review.CheckTrueFalseAnswer(true);

        Assert.False(review.CanSkip);
    }

    [Fact]
    public void Retrying_a_card_does_not_move_the_streak_but_re_scoring_it_does()
    {
        using var database = new TestDatabase();
        var review = NewReview(NewDeck("Deck", 2));

        review.Reveal();
        review.RetryLater();
        Assert.Equal(0, review.CurrentAnswerStreak);

        review.MarkCorrect();
        review.MarkIncorrect();
        Assert.Equal(0, review.CurrentAnswerStreak);
    }

    [Fact]
    public void A_second_check_of_the_same_true_false_card_does_not_change_the_result()
    {
        using var database = new TestDatabase();
        var deck = new FlashCardDeck("Deck");
        FlashCardRepository.SaveNewDeck(deck);
        foreach (var card in new[] { new TrueFalseFlashCard("Sky is blue", "", true), new TrueFalseFlashCard("Grass is red", "", false) })
        {
            FlashCardRepository.SaveNewCard(card, deck.ID);
            deck.AddFlashCard(card);
        }
        var review = NewReview(deck);
        var answerIsTrue = ((TrueFalseFlashCard)review.CurrentCard).CorrectAnswerIsTrue;

        review.CheckTrueFalseAnswer(answerIsTrue);
        review.CheckTrueFalseAnswer(!answerIsTrue);

        Assert.True(review.IsAnswerCorrect);
        Assert.Equal(1, review.CorrectCount);
    }

    [Theory]
    [InlineData("new   york", "New York")]
    [InlineData("  new york\t", "New York")]
    public void Typed_answers_ignore_extra_spacing_and_case(string typed, string answer)
    {
        using var database = new TestDatabase();
        var card = new TypeFlashCard("Biggest US city?", "", answer);
        Assert.True(card.VerifyAnswer(typed));
        Assert.False(card.VerifyAnswer("newyork"));
    }

    [Fact]
    public void A_match_card_that_keeps_its_left_order_shows_the_left_side_as_written()
    {
        using var database = new TestDatabase();
        var deck = new FlashCardDeck("Set");
        FlashCardRepository.SaveNewDeck(deck);
        List<(string leftText, string rightText)> pairs = [.. Enumerable.Range(1, 8).Select(i => ($"left {i}", $"right {i}"))];
        var card = new MatchFlashCard("Q", "Q", pairs, keepLeftOrder: true);
        FlashCardRepository.SaveNewCard(card, deck.ID);
        deck.AddFlashCard(card);

        var review = NewReview(deck);

        Assert.Equal(pairs.Select(p => p.leftText), review.MatchRows.Select(row => row.LeftText));
    }

    [Theory]
    [InlineData("Each constant goes with its first few digits.", true)]
    [InlineData(CARD_TYPE_MATCH_PLACEHOLDER, false)]
    [InlineData("", false)]
    public void A_checked_match_card_shows_its_back_unless_it_is_the_placeholder(string back, bool shown)
    {
        using var database = new TestDatabase();
        var deck = new FlashCardDeck("Set");
        FlashCardRepository.SaveNewDeck(deck);
        var card = new MatchFlashCard(CARD_TYPE_MATCH_PLACEHOLDER, back, [("a", "1"), ("b", "2")]);
        FlashCardRepository.SaveNewCard(card, deck.ID);
        deck.AddFlashCard(card);
        var review = NewReview(deck);

        review.CheckMatchAnswer();

        Assert.Equal(shown, review.ShowBackSection);
    }

    private static (FlashCardDeck deck, FlashCard card) DeckWith(FlashCard card)
    {
        var deck = new FlashCardDeck("Set");
        FlashCardRepository.SaveNewDeck(deck);
        FlashCardRepository.SaveNewCard(card, deck.ID);
        deck.AddFlashCard(card);
        return (deck, card);
    }

    private static FlashCard Edited(FlashCard card)
    {
        FlashCardRepository.UpdateCard(card);
        return FlashCardRepository.GetCardsForDeck(FlashCardRepository.GetAllDecks().Single().ID).Single(c => c.ID == card.ID);
    }

    [Fact]
    public void A_card_can_only_be_edited_once_its_answer_shows()
    {
        using var database = new TestDatabase();
        var review = NewReview(DeckWith(new FlipFlashCard("Q", "A")).deck);

        Assert.False(review.CanEditCard);
        review.Reveal();
        Assert.True(review.CanEditCard);
    }

    [Fact]
    public void Editing_the_shown_card_redraws_it_and_keeps_its_answer_showing()
    {
        using var database = new TestDatabase();
        var (deck, card) = DeckWith(new FlipFlashCard("Q", "Wrong"));
        var review = NewReview(deck);
        review.Reveal();

        review.ApplyEditedCard(Edited(new FlipFlashCard("Q", "Right", card.ID)));

        Assert.Equal("Right", review.CurrentCard.Back);
        Assert.True(review.IsAnswerRevealed);
        Assert.Equal(1, review.TotalCards);
    }

    [Fact]
    public void Making_a_card_reversible_mid_review_adds_its_reversed_question_to_the_end()
    {
        using var database = new TestDatabase();
        var (deck, card) = DeckWith(new FlipFlashCard("Q", "A"));
        var review = NewReview(deck);
        review.Reveal();

        review.ApplyEditedCard(Edited(new FlipFlashCard("Q", "A", card.ID, isReversible: true)));
        review.MarkCorrect();

        Assert.Equal(2, review.TotalCards);
        Assert.True(review.IsReversedCard);
        Assert.Equal("A", review.CurrentCard.Front);
    }

    [Fact]
    public void A_cloze_blank_added_mid_review_is_asked_and_a_removed_one_still_to_come_is_dropped()
    {
        using var database = new TestDatabase();
        var (deck, card) = DeckWith(new ClozeFlashCard(@"\C1{a} \C2{b}", "", typeAnswer: false));
        var review = NewReview(deck);
        review.Reveal();
        var shown = review.CurrentCard.ReviewPart;

        var kept = shown == "c1" ? @"\C1{a} b \C3{c}" : @"a \C2{b} \C3{c}";
        review.ApplyEditedCard(Edited(new ClozeFlashCard(kept, "", false, card.ID)));

        Assert.Equal(2, review.TotalCards);
        Assert.Equal(shown, review.CurrentCard.ReviewPart);
        review.MarkCorrect();
        Assert.Equal("c3", review.CurrentCard.ReviewPart);
    }

    [Fact]
    public void Switching_a_shown_cloze_to_typed_answers_leaves_the_shown_question_as_it_was()
    {
        using var database = new TestDatabase();
        var (deck, card) = DeckWith(new ClozeFlashCard(@"\C1{a}", "", typeAnswer: false));
        var review = NewReview(deck);
        review.Reveal();

        review.ApplyEditedCard(Edited(new ClozeFlashCard(@"\C1{a}", "", true, card.ID)));

        Assert.True(review.IsFlipCard);
        Assert.True(review.IsAnswerRevealed);
    }
}
