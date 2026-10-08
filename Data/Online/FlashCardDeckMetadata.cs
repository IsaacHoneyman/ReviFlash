using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Data.Online;

public partial class FlashCardDeckMetadata : ObservableObject, ISearchable
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("owner_id")] public Guid? OwnerId { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("storage_path")] public string? StoragePath { get; set; }
    [JsonPropertyName("card_count")] public int CardCount { get; set; }

    /// <summary> 'public' (listed in community search) or 'private' (only its owner sees it). </summary>
    [JsonPropertyName("visibility")] public string? Visibility { get; set; }

    [JsonIgnore] public bool IsPrivate => Visibility == SupabaseConnection.PrivateVisibility;
    [JsonIgnore] public string VisibilityToggleText => IsPrivate ? "Make Public" : "Make Private";

    /// <summary> Distinct signed-in users who have downloaded the deck; observable so a download updates it in place. </summary>
    [property: JsonPropertyName("download_count")]
    [NotifyPropertyChangedFor(nameof(DownloadCountText))]
    [ObservableProperty] private int _downloadCount;

    [JsonIgnore] public string DownloadCountText => DownloadCount == 1 ? "1 download" : $"{DownloadCount} downloads";
    /// <summary> The uploader's profile, embedded by the deck queries as owner:profiles(display_name). </summary>
    [JsonPropertyName("owner")] public DeckOwnerProfile? Owner { get; set; }

    [JsonIgnore] public string? UploaderName => string.IsNullOrWhiteSpace(Owner?.DisplayName) ? null : Owner.DisplayName;
    [JsonIgnore] public bool HasUploaderName => UploaderName != null;

    [JsonPropertyName("tags")] public string[]? Tags { get; set; }
    [JsonPropertyName("slug")] public string? Slug { get; set; }
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary> Placeholder description some uploaded decks carry; treated as no description. </summary>
    private const string LegacyDescription = "Uploaded via ReviFlash Desktop";

    [JsonIgnore] public bool HasDescription => !string.IsNullOrWhiteSpace(Description) && Description != LegacyDescription;

    // --- Search ---

    [JsonIgnore] public string SearchName => Title ?? string.Empty;
    [JsonIgnore] public IEnumerable<string?> SearchKeywords => [CardCount.ToString(), Description, UploaderName, .. Tags ?? []];
    [JsonIgnore] public int SortCardCount => CardCount;
    [JsonIgnore] public int SortDownloads => DownloadCount;

    // Cloud decks have no local study history, so the recency sort uses their last publish time.
    [JsonIgnore] public DateTime? SortLastActivity => (UpdatedAt ?? CreatedAt)?.UtcDateTime;
}

public sealed class DeckOwnerProfile
{
    [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
}
