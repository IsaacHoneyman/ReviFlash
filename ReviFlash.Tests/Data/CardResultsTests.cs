using System.IO.Compression;
using ReviFlash.Data.Backup.Local;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class CardResultsTests
{
    private static (FlashCardDeck deck, List<FlashCard> cards) NewDeck(string name, params FlashCard[] cards)
    {
        var deck = new FlashCardDeck(name);
        FlashCardRepository.SaveNewDeck(deck);
        foreach (var card in cards)
        {
            FlashCardRepository.SaveNewCard(card, deck.ID);
            deck.AddFlashCard(card);
        }
        return (deck, [.. cards]);
    }

    private static ReviewViewModel NewReview(FlashCardDeck deck)
    {
        MetaDataManager.Data!.ShowSkipButton = true;
        MetaDataManager.Data.ShowRetryLaterButton = true;
        return new ReviewViewModel(deck.FlashCards, deck.ID);
    }

    private static int ResultCount() => Db.Scalar<int>("SELECT COUNT(*) FROM CardResults;");

    [Fact]
    public void Every_answer_in_a_review_is_saved_against_its_card()
    {
        using var database = new TestDatabase();
        var (deck, cards) = NewDeck("Set", new FlipFlashCard("right", "a"), new FlipFlashCard("wrong", "b"));
        var review = NewReview(deck);

        for (int i = 0; i < 2; i++)
        {
            if (review.CurrentCard.Front == "right") review.MarkCorrect();
            else review.MarkIncorrect();
        }

        var right = Assert.Single(FlashCardRepository.GetCardResults(cards[0].ID));
        var wrong = Assert.Single(FlashCardRepository.GetCardResults(cards[1].ID));
        Assert.True(right.Correct);
        Assert.False(wrong.Correct);
        Assert.Equal("", right.Part);
        Assert.Equal(DateTime.Today, right.AnsweredAt.Date);
        Assert.True(right.Seconds >= 0);
    }

    [Fact]
    public void Each_cloze_blank_and_the_reversed_side_are_saved_as_their_own_part()
    {
        using var database = new TestDatabase();
        var (deck, cards) = NewDeck("Set",
            new ClozeFlashCard(@"\C1{Na} and \C2{Cl}", "", typeAnswer: false),
            new FlipFlashCard("front", "back", isReversible: true),
            new ClozeFlashCard(@"\C1{typed}", "", typeAnswer: true));
        var review = NewReview(deck);

        for (int i = 0; i < 5; i++)
        {
            if (review.IsTypeCard)
            {
                review.CheckTypedAnswer();
                review.NextCard();
            }
            else review.MarkCorrect();
        }

        Assert.Equal(["c1", "c2"], FlashCardRepository.GetCardResults(cards[0].ID).Select(r => r.Part).Order());
        Assert.Equal(["", "reversed"], FlashCardRepository.GetCardResults(cards[1].ID).Select(r => r.Part).Order());
        Assert.Equal(["c1"], FlashCardRepository.GetCardResults(cards[2].ID).Select(r => r.Part));
    }

    [Fact]
    public void A_typed_card_retried_later_has_both_answers_saved_and_a_skipped_card_none()
    {
        using var database = new TestDatabase();
        var (deck, cards) = NewDeck("Set", new TypeFlashCard("retry", "answer"), new FlipFlashCard("skip", "b"), new FlipFlashCard("other", "c"));
        var review = NewReview(deck);
        bool retried = false;

        for (int step = 0; step < 10 && ResultCount() == 0; step++)
        {
            switch (review.CurrentCard.Front)
            {
                case "skip" when review.CanSkip:
                    review.SkipCard();
                    break;
                case "retry":
                    review.UserTypedAnswer = retried ? "answer" : "wrong";
                    review.CheckTypedAnswer();
                    if (!retried && review.CanRetryLater) { retried = true; review.RetryLater(); }
                    else review.NextCard();
                    break;
                default:
                    review.MarkCorrect();
                    break;
            }
        }

        Assert.Equal([false, true], FlashCardRepository.GetCardResults(cards[0].ID).Select(r => r.Correct));
        Assert.Empty(FlashCardRepository.GetCardResults(cards[1].ID));
    }

    [Fact]
    public void Quitting_part_way_still_saves_the_answers_given()
    {
        using var database = new TestDatabase();
        var (deck, _) = NewDeck("Set", new FlipFlashCard("a", "a"), new FlipFlashCard("b", "b"));
        var review = NewReview(deck);

        review.MarkCorrect();
        review.QuitSession();

        Assert.Equal(1, ResultCount());
    }

    [Fact]
    public void Editing_a_card_keeps_its_history()
    {
        using var database = new TestDatabase();
        var (deck, cards) = NewDeck("Set", new FlipFlashCard("a", "a"));
        FlashCardRepository.UpdateDeckStats([], [new CardResult(cards[0].ID, "", DateTime.Now, true, 3)]);

        FlashCardRepository.UpdateCard(new FlipFlashCard("edited", "a", cards[0].ID));

        Assert.Single(FlashCardRepository.GetCardResults(cards[0].ID));
    }

    [Fact]
    public void Results_go_with_their_card_set_or_stats()
    {
        using var database = new TestDatabase();
        var (first, firstCards) = NewDeck("First", new FlipFlashCard("a", "a"), new FlipFlashCard("b", "b"));
        var (second, secondCards) = NewDeck("Second", new FlipFlashCard("c", "c"));
        var (third, thirdCards) = NewDeck("Third", new FlipFlashCard("d", "d"));
        FlashCardRepository.UpdateDeckStats([], [.. firstCards.Concat(secondCards).Concat(thirdCards).Select(c => new CardResult(c.ID, "", DateTime.Now, true, 1))]);

        FlashCardRepository.DeleteCard(firstCards[0].ID);
        FlashCardRepository.DeleteDeck(second.ID);
        FlashCardRepository.DeleteStatsForDeck(third.ID);

        Assert.Equal([firstCards[1].ID], Db.Query("SELECT CardID FROM CardResults;", r => (ulong)r.GetInt64(0)));

        FlashCardRepository.DeleteAllStats();
        Assert.Equal(0, ResultCount());
    }

    [Fact]
    public void An_answer_for_a_card_deleted_mid_review_is_dropped_without_losing_the_rest()
    {
        using var database = new TestDatabase();
        var (deck, cards) = NewDeck("Set", new FlipFlashCard("a", "a"), new FlipFlashCard("b", "b"));
        FlashCardRepository.DeleteCard(cards[0].ID);

        FlashCardRepository.UpdateDeckStats([(deck.ID, 2, 2, 10)],
            [new CardResult(cards[0].ID, "", DateTime.Now, true, 1), new CardResult(cards[1].ID, "", DateTime.Now, true, 1)]);

        Assert.Equal(1, ResultCount());
        Assert.Equal(2, FlashCardRepository.GetStats(deck.ID).total);
    }

    [Fact]
    public void A_backup_without_stats_leaves_out_card_results()
    {
        using var database = new TestDatabase();
        var (_, cards) = NewDeck("Set", new FlipFlashCard("a", "a"));
        FlashCardRepository.UpdateDeckStats([], [new CardResult(cards[0].ID, "", DateTime.Now, true, 1)]);

        var folder = Path.Combine(TestEnvironment.DataRoot, $"backups-{Guid.NewGuid():N}");
        BackupManager.TryCreateBackup(folder, includeStats: false);
        var extracted = Path.Combine(TestEnvironment.DataRoot, $"extracted-{Guid.NewGuid():N}.db");
        using (var archive = ZipFile.OpenRead(Directory.GetFiles(folder, "*.zip").Single())) archive.GetEntry("reviflash.db")!.ExtractToFile(extracted);

        using var connection = Db.OpenUnpooled(extracted);
        Assert.Equal(0, Db.Scalar<int>(connection, null, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'CardResults';"));
    }

    [Fact]
    public void A_full_backup_brings_card_results_back()
    {
        using var database = new TestDatabase();
        var (_, cards) = NewDeck("Set", new FlipFlashCard("a", "a"));
        FlashCardRepository.UpdateDeckStats([], [new CardResult(cards[0].ID, "c1", new DateTime(2026, 10, 1, 9, 30, 0), false, 7)]);
        var folder = Path.Combine(TestEnvironment.DataRoot, $"backups-{Guid.NewGuid():N}");
        BackupManager.TryCreateBackup(folder, includeStats: true);

        Db.Execute("DELETE FROM CardResults;");
        BackupManager.TryRestoreBackup(Directory.GetFiles(folder, "*.zip").Single());
        MetaDataManager.Data.DatabasePath = database.Path;

        Assert.Equal([new CardResult(cards[0].ID, "c1", new DateTime(2026, 10, 1, 9, 30, 0), false, 7)], FlashCardRepository.GetCardResults(cards[0].ID));
    }

    [Fact]
    public void Restoring_a_stats_less_backup_keeps_results_only_for_the_same_card()
    {
        string zip;
        using (var first = new TestDatabase())
        {
            Db.Execute("INSERT INTO Decks (Name) VALUES ('Set');");
            Db.Execute("INSERT INTO Cards (DeckID, CardType, Front, Back) VALUES (1, 'Flip', 'same', 'x'), (1, 'Flip', 'old front', 'y');");
            var backupFolder = Path.Combine(TestEnvironment.DataRoot, $"backups-{Guid.NewGuid():N}");
            BackupManager.TryCreateBackup(backupFolder, includeStats: false);
            zip = Directory.GetFiles(backupFolder, "*.zip").Single();
        }

        using var second = new TestDatabase();
        Db.Execute("INSERT INTO Decks (Name) VALUES ('Set');");
        Db.Execute("INSERT INTO Cards (DeckID, CardType, Front, Back) VALUES (1, 'Flip', 'same', 'x'), (1, 'Flip', 'new front', 'y');");
        Db.Execute("INSERT INTO CardResults (CardID, Part, AnsweredAt, Correct, Seconds) VALUES (1, '', '2026-10-01 09:00:00', 1, 2), (2, '', '2026-10-01 09:00:00', 0, 2);");

        BackupManager.TryRestoreBackup(zip);
        MetaDataManager.Data.DatabasePath = second.Path;

        Assert.Equal([1L], Db.Query("SELECT CardID FROM CardResults;", r => r.GetInt64(0)));
    }
}
