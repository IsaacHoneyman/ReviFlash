using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ReviFlash.Utilities;
using ReviFlash.Data.Local;

namespace ReviFlash.Data.Online;

/// <summary> Interface to online shared database. </summary>
public sealed class SupabaseConnection : IDisposable
{
    private const string ProjectURL = "https://hegjwggsueldwtnxpnnv.supabase.co";
    private const string AnonKey = "sb_publishable_XETpGgIYnJHFwrq28EV-4w_-aZ_UdV4";

    private readonly HttpClient _http;

    public SupabaseConnection()
    {
        var authToken = MetaDataManager.Data.SupabaseAccessToken ?? AnonKey;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("apikey", AnonKey);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        Logger.LogInfo("Supabase connection initalised.");
    }

    public void Dispose()
    {
        _http?.Dispose();
        Logger.LogInfo("Supabase connection disposed.");

    }

    // --- AUTHENTICATION METHODS ---

    public async Task<(bool Success, string Message, string? AccessToken)> SignUpAsync(string email, string password, string username)
    {
        var url = $"{ProjectURL}/auth/v1/signup";

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

    public async Task<(bool Success, string Message, string? AccessToken, string? UserId, string? Username, DateTime Expiration)> SignInAsync(string email, string password)
    {
        var url = $"{ProjectURL}/auth/v1/token?grant_type=password";
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
                int expiresIn = root.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 3600;

                string? username = "User";
                if (userNode.TryGetProperty("user_metadata", out var meta) && meta.TryGetProperty("username", out var uname))
                {
                    username = uname.GetString();
                }

                return (true, "Login successful!", token, userId, username, DateTime.Now.AddSeconds(expiresIn));
            }

            return (false, ExtractErrorMessage(json), null, null, null, DateTime.MinValue);
        }
        catch (Exception ex) { return (false, $"Network error: {ex.Message}", null, null, null, DateTime.MinValue); }
    }

    private static string ExtractErrorMessage(string json)
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

    // --- Upload ---

    public async Task<(bool Success, string Message)> UploadCloudDeckAsync(string userId, string title, int cardCount, string jsonPayload)
    {
        string fileName = $"{userId}/{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_export.json";

        string storageUrl = $"{ProjectURL}/storage/v1/object/decks/{fileName}";
        var storageContent = new StringContent(jsonPayload, Encoding.UTF8);
        storageContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            using var storageRes = await _http.PostAsync(storageUrl, storageContent).ConfigureAwait(false);
            if (!storageRes.IsSuccessStatusCode)
                return (false, $"Storage Error: {storageRes.StatusCode} - {await storageRes.Content.ReadAsStringAsync()}");


            string dbUrl = $"{ProjectURL}/rest/v1/decks";
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
            _http.DefaultRequestHeaders.Remove("Prefer");

            if (dbRes.IsSuccessStatusCode) return (true, "Deck uploaded successfully!");

            return (false, $"Database Error: {dbRes.StatusCode} - {await dbRes.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex) { return (false, $"Network error: {ex.Message}"); }
    }

    public async Task<(bool Success, string Message)> UpdateCloudDeckAsync(string storagePath, string title, int cardCount, string jsonPayload)
    {
        try
        {
            string storageUrl = $"{ProjectURL}/storage/v1/object/decks/{storagePath}";
            var storageContent = new StringContent(jsonPayload, Encoding.UTF8);
            storageContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var storageReq = new HttpRequestMessage(HttpMethod.Put, storageUrl) { Content = storageContent };
            using var storageRes = await _http.SendAsync(storageReq).ConfigureAwait(false);

            if (!storageRes.IsSuccessStatusCode)
                return (false, $"Storage Update Error: {storageRes.StatusCode} - {await storageRes.Content.ReadAsStringAsync()}");


            string dbUrl = $"{ProjectURL}/rest/v1/decks?storage_path=eq.{Uri.EscapeDataString(storagePath)}";
            var dbPayload = new
            {
                title = title,
                card_count = cardCount,
                updated_at = DateTimeOffset.UtcNow.ToString("o")
            };

            var dbContent = new StringContent(JsonSerializer.Serialize(dbPayload), Encoding.UTF8, "application/json");
            var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), dbUrl) { Content = dbContent };

            using var dbRes = await _http.SendAsync(patchReq).ConfigureAwait(false);

            if (dbRes.IsSuccessStatusCode) return (true, "Deck updated successfully!");
            return (false, $"Database Update Error: {dbRes.StatusCode} - {await dbRes.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteCloudDeckAsync(string storagePath)
    {
        try
        {
            string storageUrl = $"{ProjectURL}/storage/v1/object/decks/{storagePath}";
            using var storageRes = await _http.DeleteAsync(storageUrl).ConfigureAwait(false);

            if (!storageRes.IsSuccessStatusCode)
            {
                var storageErr = await storageRes.Content.ReadAsStringAsync();
                return (false, $"Storage Delete Error: {storageRes.StatusCode} - {storageErr}");
            }

            string dbUrl = $"{ProjectURL}/rest/v1/decks?storage_path=eq.{Uri.EscapeDataString(storagePath)}";
            using var dbRes = await _http.DeleteAsync(dbUrl).ConfigureAwait(false);

            if (dbRes.IsSuccessStatusCode) return (true, "Deck deleted successfully.");

            return (false, $"Database delete failed: {await dbRes.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    // --- Find --- 

    public async Task<string> DownloadCloudDeckJsonAsync(string storagePath, string bucket = "decks")
    {
        if (string.IsNullOrWhiteSpace(storagePath)) throw new ArgumentNullException(nameof(storagePath));
        var key = storagePath.StartsWith(bucket + "/", StringComparison.OrdinalIgnoreCase)
            ? storagePath[(bucket.Length + 1)..]
            : storagePath;

        var url = $"{ProjectURL}/storage/v1/object/public/{bucket}/{Uri.EscapeDataString(key)}";
        using var res = await _http.GetAsync(url).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public async Task<List<FlashCardDeckMetadata>> GetUserCloudDecksAsync(string userId)
    {
        var url = $"{ProjectURL}/rest/v1/decks?select=id,title,description,storage_path,card_count,version,created_at,updated_at&storage_path=ilike.{userId}/*";

        using var res = await _http.GetAsync(url).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return [];

        return JsonSerializer.Deserialize<List<FlashCardDeckMetadata>>(
            await res.Content.ReadAsStringAsync().ConfigureAwait(false), TextUtility.CaseInsensitive) ?? [];
    }

    public async Task<List<FlashCardDeckMetadata>> GetPublicDecksAsync(string searchText = "", int limit = 25)
    {
        var url = $"{ProjectURL}/rest/v1/decks?select=id,title,description,storage_path,card_count,version,created_at,updated_at&visibility=eq.public";
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var escapedSearch = Uri.EscapeDataString($"*{searchText}*");
            url += $"&title=ilike.{escapedSearch}";
        }
        url += $"&limit={limit}";

        using var res = await _http.GetAsync(url).ConfigureAwait(false);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"Database Error ({res.StatusCode}): {await res.Content.ReadAsStringAsync()}");

        return JsonSerializer.Deserialize<List<FlashCardDeckMetadata>>(
            await res.Content.ReadAsStringAsync().ConfigureAwait(false), TextUtility.CaseInsensitive) ?? [];
    }
}