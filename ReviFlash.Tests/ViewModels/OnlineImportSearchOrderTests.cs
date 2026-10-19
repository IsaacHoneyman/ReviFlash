using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class OnlineImportSearchOrderTests
{
    private static List<FlashCardDeckMetadata> Results(params string[] titles) =>
        [.. titles.Select(title => new FlashCardDeckMetadata { Id = Guid.NewGuid(), Title = title })];

    [Fact]
    public async Task An_older_search_finishing_last_does_not_overwrite_a_newer_one()
    {
        using var database = new TestDatabase();
        var vm = new OnlineImportViewModel();
        var older = new TaskCompletionSource<List<FlashCardDeckMetadata>>();

        var first = vm.BeginSearch();
        var olderSearch = vm.ApplySearchAsync(first, "old", "set", () => older.Task);
        var second = vm.BeginSearch();
        await vm.ApplySearchAsync(second, "new", "set", () => Task.FromResult(Results("New")));

        older.SetResult(Results("Old one", "Old two"));
        await olderSearch;

        Assert.Equal(["New"], vm.SearchResults.Select(deck => deck.Title));
    }

    [Fact]
    public async Task An_older_search_that_fails_is_ignored()
    {
        using var database = new TestDatabase();
        var vm = new OnlineImportViewModel();
        var older = new TaskCompletionSource<List<FlashCardDeckMetadata>>();

        var first = vm.BeginSearch();
        var olderSearch = vm.ApplySearchAsync(first, "old", "set", () => older.Task);
        var second = vm.BeginSearch();
        await vm.ApplySearchAsync(second, "new", "set", () => Task.FromResult(Results("New")));

        older.SetException(new InvalidOperationException("offline"));
        await olderSearch;

        Assert.Equal(["New"], vm.SearchResults.Select(deck => deck.Title));
    }

    [Fact]
    public async Task The_latest_search_still_applies_and_still_fails_loudly()
    {
        using var database = new TestDatabase();
        var vm = new OnlineImportViewModel();

        var generation = vm.BeginSearch();
        await vm.ApplySearchAsync(generation, "", "set", () => Task.FromResult(Results("A", "B")));
        Assert.Equal(2, vm.SearchResults.Count);

        var latest = vm.BeginSearch();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            vm.ApplySearchAsync(latest, "b", "set", () => throw new InvalidOperationException("offline")));
    }
}
