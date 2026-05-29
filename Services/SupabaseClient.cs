using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using ReviFlash.Models;

namespace ReviFlash.Services
{
    /// <summary>
    /// Minimal Supabase client boilerplate. Configure keys in <see cref="SupabaseConfig"/>.
    /// Provides simple public-read operations used by the app later.
    /// </summary>
    public sealed class SupabaseClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _projectUrl;

        public SupabaseClient()
        {
            _projectUrl = SupabaseConfig.ProjectUrl?.TrimEnd('/') ?? throw new InvalidOperationException("Supabase ProjectUrl not configured.");
            var anon = SupabaseConfig.AnonKey ?? throw new InvalidOperationException("Supabase anon key not configured.");

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("apikey", anon);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", anon);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<DeckMetadata>> GetPublicDecksAsync()
        {
            var url = $"{_projectUrl}/rest/v1/decks?select=id,title,description,storage_path,card_count,tags,slug,version,created_at,updated_at&visibility=eq.public";
            using var res = await _http.GetAsync(url).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<DeckMetadata>>(json, opts) ?? new List<DeckMetadata>();
        }

        public async Task<string> DownloadDeckJsonAsync(string storagePath, string bucket = "decks")
        {
            if (string.IsNullOrWhiteSpace(storagePath)) throw new ArgumentNullException(nameof(storagePath));
            var key = storagePath.StartsWith(bucket + "/", StringComparison.OrdinalIgnoreCase)
                ? storagePath.Substring(bucket.Length + 1)
                : storagePath;

            var url = $"{_projectUrl}/storage/v1/object/public/{bucket}/{Uri.EscapeDataString(key)}";
            using var res = await _http.GetAsync(url).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
