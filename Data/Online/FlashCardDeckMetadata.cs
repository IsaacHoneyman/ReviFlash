using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ReviFlash.Utilities;

namespace ReviFlash.Data.Online;

public class FlashCardDeckMetadata : ISearchable
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("storage_path")] public string? StoragePath { get; set; }
    [JsonPropertyName("card_count")] public int CardCount { get; set; }
    [JsonPropertyName("tags")] public string[]? Tags { get; set; }
    [JsonPropertyName("slug")] public string? Slug { get; set; }
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }

    // --- Search ---

    [JsonIgnore] public string SearchName => Title ?? string.Empty;
    [JsonIgnore] public IEnumerable<string?> SearchKeywords => [CardCount.ToString(), Description, .. Tags ?? []];
    [JsonIgnore] public int SortCardCount => CardCount;

    // Cloud decks carry no local study history, so the recency sort uses when they were
    // last published instead.
    [JsonIgnore] public DateTime? SortLastActivity => (UpdatedAt ?? CreatedAt)?.UtcDateTime;
}

