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

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("apikey", anon);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", anon);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // --- AUTHENTICATION METHODS ---

        // Add the username parameter here:
        public async Task<(bool Success, string Message, string? AccessToken)> SignUpAsync(string email, string password, string username)
        {
            var url = $"{_projectUrl}/auth/v1/signup";

            // Add the "data" object. Supabase automatically converts this to raw_user_meta_data!
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

        public async Task<(bool Success, string Message, string? AccessToken)> SignInAsync(string email, string password)
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
                    var token = doc.RootElement.GetProperty("access_token").GetString();
                    return (true, "Login successful!", token);
                }

                return (false, ExtractErrorMessage(json), null);
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}", null);
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