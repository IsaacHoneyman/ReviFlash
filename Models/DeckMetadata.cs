using System;
using System.Text.Json.Serialization;

namespace ReviFlash.Models
{
    public class DeckMetadata
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
    }
}
