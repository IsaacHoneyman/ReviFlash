using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text; // <-- This was missing! Required for Encoding.UTF8
using System.Text.Json;
using System.Threading.Tasks;
using ReviFlash.Models;

namespace ReviFlash.Services
{
    /// <summary>
    /// Minimal Supabase client boilerplate. Configure keys in <see cref="SupabaseConfig"/>.
    /// Provides simple public-read operations and authentication.
    /// </summary>
    public sealed class SupabaseClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _projectUrl;

        public SupabaseClient()
        {
            _projectUrl = SupabaseConfig.ProjectUrl?.TrimEnd('/') ?? throw new InvalidOperationException("Supabase ProjectUrl not configured.");
            var anon = SupabaseConfig.AnonKey ?? throw new InvalidOperationException("Supabase anon key not configured.");
            var authToken = SupabaseConfig.CurrentAccessToken ?? anon;

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("apikey", anon);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // --- AUTHENTICATION METHODS ---

        public async Task<(bool Success, string Message, string? AccessToken)> SignUpAsync(string email, string password, string username)
        {
            var url = $"{_projectUrl}/auth/v1/signup";

            var payload = new
            {
                email,
                password,
                data = new { username }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                using var res = await _http.PostAsync(url, content).ConfigureAwait(false);
                var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (res.IsSuccessStatusCode)
                {
                    return (true, "Account created! Check your email to confirm.", null);
                }

                return (false, ExtractErrorMessage(json), null);
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, string? AccessToken, string? UserId, string? Username)> SignInAsync(string email, string password)
        {
            var url = $"{_projectUrl}/auth/v1/token?grant_type=password";
            var payload = new { email, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                using var res = await _http.PostAsync(url, content).ConfigureAwait(false);
                var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var token = root.GetProperty("access_token").GetString();
                    var userNode = root.GetProperty("user");
                    var userId = userNode.GetProperty("id").GetString();

                    string? username = "User";
                    if (userNode.TryGetProperty("user_metadata", out var meta) && meta.TryGetProperty("username", out var uname))
                    {
                        username = uname.GetString();
                    }

                    return (true, "Login successful!", token, userId, username);
                }

                return (false, ExtractErrorMessage(json), null, null, null);
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}", null, null, null);
            }
        }

        private string ExtractErrorMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error_description", out var desc)) return desc.GetString() ?? "Unknown error";
                if (doc.RootElement.TryGetProperty("msg", out var msg)) return msg.GetString() ?? "Unknown error";
            }
            catch { /* Ignore parsing errors */ }
            return "Authentication failed.";
        }

        // --- DATABASE / STORAGE METHODS ---

        public async Task<List<DeckMetadata>> GetPublicDecksAsync(string searchText = "", int limit = 25)
        {
            var url = $"{_projectUrl}/rest/v1/decks?select=id,title,description,storage_path,card_count,version,created_at,updated_at&visibility=eq.public";

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var escapedSearch = Uri.EscapeDataString($"*{searchText}*");
                url += $"&title=ilike.{escapedSearch}";
            }

            url += $"&limit={limit}";

            using var res = await _http.GetAsync(url).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                var errorContent = await res.Content.ReadAsStringAsync();
                throw new Exception($"Database Error ({res.StatusCode}): {errorContent}");
            }

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

        public async Task<(bool Success, string Message)> UploadDeckAsync(string userId, string title, int cardCount, string jsonPayload)
        {
            string fileName = $"{userId}/{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_export.json";

            string storageUrl = $"{_projectUrl}/storage/v1/object/decks/{fileName}";
            var storageContent = new StringContent(jsonPayload, Encoding.UTF8);
            storageContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            try
            {
                using var storageRes = await _http.PostAsync(storageUrl, storageContent).ConfigureAwait(false);
                if (!storageRes.IsSuccessStatusCode)
                {
                    var storageErr = await storageRes.Content.ReadAsStringAsync();
                    return (false, $"Storage Error: {storageRes.StatusCode} - {storageErr}");
                }

                string dbUrl = $"{_projectUrl}/rest/v1/decks";
                var dbPayload = new
                {
                    title = title,
                    description = "Uploaded via ReviFlash Desktop",
                    storage_path = fileName,
                    card_count = cardCount
                };

                var dbContent = new StringContent(JsonSerializer.Serialize(dbPayload), Encoding.UTF8, "application/json");
                _http.DefaultRequestHeaders.Add("Prefer", "return=minimal");

                using var dbRes = await _http.PostAsync(dbUrl, dbContent).ConfigureAwait(false);
                _http.DefaultRequestHeaders.Remove("Prefer"); // Clean up header

                if (dbRes.IsSuccessStatusCode)
                {
                    return (true, "Deck uploaded successfully!");
                }

                var dbErr = await dbRes.Content.ReadAsStringAsync();
                return (false, $"Database Error: {dbRes.StatusCode} - {dbErr}");
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}