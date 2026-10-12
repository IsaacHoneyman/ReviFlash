using System.IO.Compression;
using ReviFlash.Data.Backup.Local;
using ReviFlash.Data.Local;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class BackupRestoreTests
{
    private static string NewBackupFolder() => Path.Combine(TestEnvironment.DataRoot, $"backups-{Guid.NewGuid():N}");

    private static string CreateStatsLessBackup()
    {
        var folder = NewBackupFolder();
        BackupManager.TryCreateBackup(folder, includeStats: false);
        return Directory.GetFiles(folder, "*.zip").Single();
    }

    private static void Restore(string zip, TestDatabase live)
    {
        BackupManager.TryRestoreBackup(zip);
        MetaDataManager.Data.DatabasePath = live.Path;
    }

    [Fact]
    public void Restoring_a_stats_less_backup_drops_stats_for_a_different_set_with_the_same_id()
    {
        string zip;
        using (var first = new TestDatabase())
        {
            Db.Execute("INSERT INTO Decks (Name) VALUES ('Biology'), ('Shared');");
            zip = CreateStatsLessBackup();
        }

        using var second = new TestDatabase();
        Db.Execute("INSERT INTO Decks (Name) VALUES ('Chemistry'), ('Shared');");
        Db.Execute("INSERT INTO DeckStats (DeckId, CorrectCount, TotalAttempts, TimeTakenSeconds, DateChecked) VALUES (1, 1, 1, 1, '2026-01-01'), (2, 5, 5, 5, '2026-01-01');");
        Db.Execute("INSERT INTO AnswerStreaks (TargetType, TargetId, BestStreak) VALUES ('Deck', 1, 9), ('Deck', 2, 7);");

        Restore(zip, second);

        Assert.Equal([2L], Db.Query("SELECT DeckId FROM DeckStats;", r => r.GetInt64(0)));
        Assert.Equal([2L], Db.Query("SELECT TargetId FROM AnswerStreaks WHERE TargetType = 'Deck';", r => r.GetInt64(0)));
    }

    [Fact]
    public void Restoring_a_stats_less_backup_keeps_group_streaks_only_when_id_and_name_match()
    {
        string zip;
        using (var first = new TestDatabase())
        {
            Db.Execute("INSERT INTO StudyGroups (Name) VALUES ('Exam'), ('Revision');");
            zip = CreateStatsLessBackup();
        }

        using var second = new TestDatabase();
        Db.Execute("INSERT INTO StudyGroups (Name) VALUES ('Other'), ('Revision');");
        Db.Execute("INSERT INTO AnswerStreaks (TargetType, TargetId, BestStreak) VALUES ('Group', 1, 3), ('Group', 2, 4);");

        Restore(zip, second);

        Assert.Equal([2L], Db.Query("SELECT TargetId FROM AnswerStreaks WHERE TargetType = 'Group';", r => r.GetInt64(0)));
    }

    [Fact]
    public void Restoring_a_stats_less_backup_moves_note_stats_by_sync_id()
    {
        string zip;
        string sharedSyncId;
        using (var first = new TestDatabase())
        {
            Db.Execute("INSERT INTO Notes (SyncID, Name, CreatedAt, UpdatedAt) VALUES ('other-1', 'A', 'x', 'x'), ('shared', 'B', 'x', 'x');");
            zip = CreateStatsLessBackup();
            sharedSyncId = "shared";
        }

        using var second = new TestDatabase();
        Db.Execute("INSERT INTO Notes (SyncID, Name, CreatedAt, UpdatedAt) VALUES ('shared', 'B', 'x', 'x'), ('other-2', 'C', 'x', 'x');");
        Db.Execute("INSERT INTO NoteStats (NoteID, DateStudied, Seconds) VALUES (1, '2026-01-01', 10), (2, '2026-01-01', 20);");

        Restore(zip, second);

        var rows = Db.Query("SELECT n.SyncID, s.Seconds FROM NoteStats s JOIN Notes n ON n.ID = s.NoteID;", r => (r.GetString(0), r.GetInt32(1)));
        Assert.Equal([(sharedSyncId, 10)], rows);
    }
}
