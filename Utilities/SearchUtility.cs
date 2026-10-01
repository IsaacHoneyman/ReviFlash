using System;
using System.Collections.Generic;
using System.Linq;

namespace ReviFlash.Utilities;

/// <summary>
/// Describes anything that can appear in a searchable list. Implemented by decks,
/// study groups, folders and cloud deck metadata so every list in the app ranks and
/// sorts results identically.
/// </summary>
public interface ISearchable
{
    /// <summary> The primary text a search matches against, and the name sorts use. </summary>
    string SearchName { get; }

    /// <summary> Secondary text (card counts, descriptions, folder paths) matched at reduced weight. </summary>
    IEnumerable<string?> SearchKeywords => [];

    /// <summary> Number of cards, for the card count sorts. </summary>
    int SortCardCount => 0;

    /// <summary> Recorded study time in seconds, for the study time sorts. </summary>
    int SortStudySeconds => 0;

    /// <summary> When the item was last touched, for the recency sort. Null sorts last. </summary>
    DateTime? SortLastActivity => null;

    /// <summary> Download count, for the popularity sort on cloud decks. </summary>
    int SortDownloads => 0;
}

/// <summary> The orderings every searchable list in the app offers. </summary>
public enum SortMode
{
    Relevance,
    NameAscending,
    NameDescending,
    CardsDescending,
    CardsAscending,
    StudyTimeDescending,
    StudyTimeAscending,
    RecentlyStudied,
    DownloadsDescending,
}

/// <summary> A sort choice bound to a ComboBox. </summary>
public sealed class SortOption(string label, SortMode mode)
{
    public string Label { get; } = label;
    public SortMode Mode { get; } = mode;

    public override string ToString() => Label;
}

/// <summary>
/// Relevance scoring and ordering shared by the dashboard, the study group editor and
/// the online import/export browsers.
/// </summary>
public static class SearchUtility
{
    /// <summary> Returned by <see cref="ScoreText"/> when the text does not match at all. </summary>
    public const int NoMatch = -1;

    // Match tiers, best first. The gaps leave room for the per-tier quality bonuses below.
    private const int ExactTier = 10_000;
    private const int PrefixTier = 8_000;
    private const int WordPrefixTier = 6_000;
    private const int ContainsTier = 4_000;
    private const int AllTokensTier = 2_000;
    private const int SubsequenceTier = 1_000;

    // Secondary fields (card counts, descriptions) match, but never outrank a name match.
    private const double KeywordWeight = 0.35;

    private static readonly char[] WordSeparators = [' ', '\t', '-', '_', '/', '\\', '.', ',', ':', ';', '(', ')', '[', ']'];

    public static readonly IReadOnlyList<SortOption> SortOptions =
    [
        new("Best match", SortMode.Relevance),
        new("Name A-Z", SortMode.NameAscending),
        new("Name Z-A", SortMode.NameDescending),
        new("Cards (high → low)", SortMode.CardsDescending),
        new("Cards (low → high)", SortMode.CardsAscending),
        new("Study time (high → low)", SortMode.StudyTimeDescending),
        new("Study time (low → high)", SortMode.StudyTimeAscending),
        new("Recently studied", SortMode.RecentlyStudied),
    ];

    /// <summary> A fresh bindable copy of the shared sort options, one per view. </summary>
    public static List<SortOption> CreateSortOptions() => [.. SortOptions];

    /// <summary>
    /// Orderings for cloud decks: popularity first, and no study-time sorts, since cloud
    /// decks carry no study history (their "recent" means recently updated).
    /// </summary>
    public static List<SortOption> CreateCloudSortOptions() =>
    [
        new("Most downloaded", SortMode.DownloadsDescending),
        new("Best match", SortMode.Relevance),
        new("Name A-Z", SortMode.NameAscending),
        new("Name Z-A", SortMode.NameDescending),
        new("Cards (high → low)", SortMode.CardsDescending),
        new("Cards (low → high)", SortMode.CardsAscending),
        new("Recently updated", SortMode.RecentlyStudied),
    ];

    public static bool IsEmptyQuery(string? query) => string.IsNullOrWhiteSpace(query);

    /// <summary>
    /// Scores a single piece of text against a query. Higher is better;
    /// <see cref="NoMatch"/> means the text should be filtered out.
    /// </summary>
    public static int ScoreText(string? candidate, string? query)
    {
        if (IsEmptyQuery(query)) return 0;
        if (string.IsNullOrEmpty(candidate)) return NoMatch;

        var text = candidate.Trim();
        var needle = query!.Trim();

        if (text.Equals(needle, StringComparison.OrdinalIgnoreCase))
        {
            return ExactTier;
        }

        if (text.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
        {
            return PrefixTier + LengthBonus(text);
        }

        int wordPrefixIndex = IndexOfWordPrefix(text, needle);
        if (wordPrefixIndex >= 0)
        {
            return WordPrefixTier + PositionBonus(wordPrefixIndex) + LengthBonus(text);
        }

        int containsIndex = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (containsIndex >= 0)
        {
            return ContainsTier + PositionBonus(containsIndex) + LengthBonus(text);
        }

        if (MatchesAllTokens(text, needle))
        {
            return AllTokensTier + LengthBonus(text);
        }

        if (IsSubsequence(text, needle))
        {
            return SubsequenceTier + LengthBonus(text);
        }

        return NoMatch;
    }

    /// <summary>
    /// Scores an item across its name and keywords, taking its best field match.
    /// </summary>
    public static int ScoreItem(ISearchable item, string? query)
    {
        if (IsEmptyQuery(query)) return 0;

        int best = ScoreText(item.SearchName, query);

        foreach (var keyword in item.SearchKeywords)
        {
            int keywordScore = ScoreText(keyword, query);
            if (keywordScore == NoMatch) continue;

            int weighted = (int)(keywordScore * KeywordWeight);
            if (weighted > best) best = weighted;
        }

        return best;
    }

    /// <summary> Filters out non-matches, keeping the input order. </summary>
    public static IEnumerable<T> Filter<T>(this IEnumerable<T> items, string? query) where T : ISearchable
    {
        if (IsEmptyQuery(query)) return items;
        return items.Where(item => ScoreItem(item, query) != NoMatch);
    }

    /// <summary>
    /// The one entry point every list uses: drops non-matches, then orders what is left
    /// by the chosen sort. <paramref name="typePriority"/> lets a mixed list (folders,
    /// groups, sets) keep its type grouping when not sorting by relevance.
    /// </summary>
    public static List<T> SearchAndSort<T>(
        this IEnumerable<T> items,
        string? query,
        SortMode sortMode,
        Func<T, int>? typePriority = null) where T : ISearchable
    {
        bool hasQuery = !IsEmptyQuery(query);

        var scored = items
            .Select(item => (Item: item, Score: hasQuery ? ScoreItem(item, query) : 0))
            .Where(entry => entry.Score != NoMatch)
            .ToList();

        // "Best match" only means anything while there is a query to match against;
        // without one it falls back to A-Z so the list never looks arbitrary.
        var effectiveMode = sortMode == SortMode.Relevance && !hasQuery ? SortMode.NameAscending : sortMode;

        IOrderedEnumerable<(T Item, int Score)> ordered;

        if (effectiveMode == SortMode.Relevance)
        {
            ordered = scored
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => typePriority?.Invoke(entry.Item) ?? 0)
                .ThenBy(entry => entry.Item.SearchName, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var grouped = scored.OrderBy(entry => typePriority?.Invoke(entry.Item) ?? 0);

            ordered = effectiveMode switch
            {
                SortMode.NameAscending => grouped.ThenBy(e => e.Item.SearchName, StringComparer.OrdinalIgnoreCase),
                SortMode.NameDescending => grouped.ThenByDescending(e => e.Item.SearchName, StringComparer.OrdinalIgnoreCase),
                SortMode.CardsDescending => grouped.ThenByDescending(e => e.Item.SortCardCount),
                SortMode.CardsAscending => grouped.ThenBy(e => e.Item.SortCardCount),
                SortMode.StudyTimeDescending => grouped.ThenByDescending(e => e.Item.SortStudySeconds),
                SortMode.StudyTimeAscending => grouped.ThenBy(e => e.Item.SortStudySeconds),
                // Never-studied items have no date at all; park them at the end rather
                // than letting DateTime.MinValue interleave with real timestamps.
                SortMode.DownloadsDescending => grouped.ThenByDescending(e => e.Item.SortDownloads),
                SortMode.RecentlyStudied => grouped
                    .ThenBy(e => e.Item.SortLastActivity.HasValue ? 0 : 1)
                    .ThenByDescending(e => e.Item.SortLastActivity ?? DateTime.MinValue),
                _ => grouped.ThenBy(e => e.Item.SearchName, StringComparer.OrdinalIgnoreCase),
            };

            // Relevance still breaks ties, so an explicit sort never scrambles equal rows.
            ordered = ordered
                .ThenByDescending(e => e.Score)
                .ThenBy(e => e.Item.SearchName, StringComparer.OrdinalIgnoreCase);
        }

        return [.. ordered.Select(entry => entry.Item)];
    }

    // --- Scoring helpers ---

    // Shorter names are the better match for the same tier: "Cells" beats "Cells of the Liver".
    private static int LengthBonus(string text) => Math.Max(0, 100 - Math.Min(text.Length, 100));

    // An earlier match is a better match.
    private static int PositionBonus(int index) => Math.Max(0, 200 - Math.Min(index, 200)) * 2;

    /// <summary> Index of a word in <paramref name="text"/> that starts with <paramref name="needle"/>. </summary>
    private static int IndexOfWordPrefix(string text, string needle)
    {
        int index = 0;

        while (index < text.Length)
        {
            int next = text.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase);
            if (next < 0) return -1;

            if (next == 0 || WordSeparators.Contains(text[next - 1])) return next;

            index = next + 1;
        }

        return -1;
    }

    /// <summary> Every whitespace separated token in the query appears somewhere in the text. </summary>
    private static bool MatchesAllTokens(string text, string needle)
    {
        var tokens = needle.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return false;

        return tokens.All(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary> Typo tolerant last resort: "bch" still finds "Biochemistry". </summary>
    private static bool IsSubsequence(string text, string needle)
    {
        int textIndex = 0;

        foreach (char needleChar in needle)
        {
            if (char.IsWhiteSpace(needleChar)) continue;

            bool found = false;
            while (textIndex < text.Length)
            {
                if (char.ToLowerInvariant(text[textIndex++]) == char.ToLowerInvariant(needleChar))
                {
                    found = true;
                    break;
                }
            }

            if (!found) return false;
        }

        return true;
    }
}
