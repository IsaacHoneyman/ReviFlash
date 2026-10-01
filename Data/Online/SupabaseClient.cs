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

    public const string PublicVisibility = "public";
    public const string PrivateVisibility = "private";

    private readonly HttpClient _http;

    public SupabaseConnection()
    {
        // An expired session token is rejected outright (401), even for public reads, so
        // fall back to the anon key once it lapses.
        var data = MetaDataManager.Data;
        var authToken = !string.IsNullOrEmpty(data.SupabaseAccessToken) && data.SupabaseExpirationTime > DateTime.Now
            ? data.SupabaseAccessToken
            : AnonKey;

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

    /// <summary>
    /// A connection carrying the current session, refreshed first if it has lapsed. Use this
    /// for everything except the auth calls, which work fine with the anon key.
    /// </summary>
    public static async Task<SupabaseConnection> CreateAsync()
    {
        // No ConfigureAwait(false): a refresh updates the metadata, which belongs to the UI thread.
        await AuthSession.TryRestoreAsync();
        return new SupabaseConnection();
    }

    /// <summary> Creates an account. Session is set only when the project skips email confirmation. </summary>
    public async Task<AuthResult> SignUpAsync(string email, string password, string username)
    {
        var payload = new { email, password, data = new { username } };
        var (ok, json, _) = await PostAuthAsync("signup", payload).ConfigureAwait(false);
        if (!ok) return AuthResult.Fail(ExtractErrorMessage(json));

        var session = TryParseSession(json);
        return session is null
            ? AuthResult.Ok("Account created! Check your email to confirm it, then sign in.")
            : AuthResult.Ok("Account created!", session);
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        var (ok, json, _) = await PostAuthAsync("token?grant_type=password", new { email, password }).ConfigureAwait(false);
        if (!ok) return AuthResult.Fail(ExtractErrorMessage(json));

        var session = TryParseSession(json);
        return session is null ? AuthResult.Fail("Unexpected response from the server.") : AuthResult.Ok("Signed in.", session);
    }

    /// <summary> Swaps a refresh token for a fresh session. <see cref="AuthResult.Rejected"/> means sign in again. </summary>
    public async Task<AuthResult> RefreshSessionAsync(string refreshToken)
    {
        var (ok, json, status) = await PostAuthAsync("token?grant_type=refresh_token", new { refresh_token = refreshToken }).ConfigureAwait(false);
        // Only a 4xx means the token itself is dead (revoked, reused, signed out elsewhere).
        // Rate limits and server errors leave the session in place to retry later.
        if (!ok)
        {
            bool rejected = status is >= 400 and < 500 and not 408 and not 429;
            return AuthResult.Fail(ExtractErrorMessage(json), rejected);
        }

        var session = TryParseSession(json);
        return session is null ? AuthResult.Fail("Unexpected response from the server.") : AuthResult.Ok("Session refreshed.", session);
    }

    /// <summary> Emails a one-time recovery code (the Reset Password template must include <c>{{ .Token }}</c>). </summary>
    public async Task<AuthResult> RequestPasswordResetAsync(string email)
    {
        var (ok, json, _) = await PostAuthAsync("recover", new { email }).ConfigureAwait(false);
        return ok
            ? AuthResult.Ok($"If an account exists for {email}, a reset code is on its way.")
            : AuthResult.Fail(ExtractErrorMessage(json));
    }

    /// <summary> Trades the emailed recovery code for a session that may set a new password. </summary>
    public async Task<AuthResult> VerifyRecoveryCodeAsync(string email, string code)
    {
        var (ok, json, _) = await PostAuthAsync("verify", new { type = "recovery", email, token = code }).ConfigureAwait(false);
        if (!ok) return AuthResult.Fail(ExtractErrorMessage(json));

        var session = TryParseSession(json);
        return session is null ? AuthResult.Fail("Unexpected response from the server.") : AuthResult.Ok("Code accepted.", session);
    }

    /// <summary> Sets a new password for the signed-in user (this connection's session). </summary>
    public async Task<AuthResult> UpdatePasswordAsync(string newPassword)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(new { password = newPassword }), Encoding.UTF8, "application/json");
            using var res = await _http.PutAsync($"{ProjectURL}/auth/v1/user", content).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

            return res.IsSuccessStatusCode ? AuthResult.Ok("Password updated.") : AuthResult.Fail(ExtractErrorMessage(json));
        }
        catch (Exception ex) { return AuthResult.Fail($"Network error: {ex.Message}"); }
    }

    /// <summary>
    /// Renames the signed-in user: their profile (what community decks show) and their auth
    /// metadata (where sign-in reads the name from), so the two never disagree.
    /// </summary>
    public async Task<AuthResult> UpdateUsernameAsync(string userId, string username)
    {
        try
        {
            var profileReq = new HttpRequestMessage(new HttpMethod("PATCH"), $"{ProjectURL}/rest/v1/profiles?id=eq.{Uri.EscapeDataString(userId)}")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { display_name = username }), Encoding.UTF8, "application/json")
            };
            using (var profileRes = await _http.SendAsync(profileReq).ConfigureAwait(false))
            {
                if (!profileRes.IsSuccessStatusCode)
                    return AuthResult.Fail(ExtractErrorMessage(await profileRes.Content.ReadAsStringAsync().ConfigureAwait(false)));
            }

            var metadata = new StringContent(JsonSerializer.Serialize(new { data = new { username } }), Encoding.UTF8, "application/json");
            using var userRes = await _http.PutAsync($"{ProjectURL}/auth/v1/user", metadata).ConfigureAwait(false);
            if (!userRes.IsSuccessStatusCode)
                return AuthResult.Fail(ExtractErrorMessage(await userRes.Content.ReadAsStringAsync().ConfigureAwait(false)));

            return AuthResult.Ok("Username updated.");
        }
        catch (Exception ex) { return AuthResult.Fail($"Network error: {ex.Message}"); }
    }

    /// <summary> Revokes this connection's session on the server. Best effort: failures are only logged. </summary>
    public async Task SignOutAsync()
    {
        try
        {
            using var res = await _http.PostAsync($"{ProjectURL}/auth/v1/logout", null).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) Logger.LogInfo($"Supabase logout returned {res.StatusCode}.");
        }
        catch (Exception ex) { Logger.LogInfo($"Supabase logout failed: {ex.Message}"); }
    }

    /// <summary> Posts to an auth endpoint. The JSON is null (and the status 0) only when the request never reached the server. </summary>
    private async Task<(bool Ok, string? Json, int Status)> PostAuthAsync(string endpoint, object payload)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var res = await _http.PostAsync($"{ProjectURL}/auth/v1/{endpoint}", content).ConfigureAwait(false);
            return (res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync().ConfigureAwait(false), (int)res.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.LogInfo($"Supabase auth request '{endpoint}' failed: {ex.Message}");
            return (false, null, 0);
        }
    }

    private static AuthSessionData? TryParseSession(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenProp) ||
                !root.TryGetProperty("refresh_token", out var refreshProp) ||
                !root.TryGetProperty("user", out var userNode)) return null;

            int expiresIn = root.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 3600;

            string username = "User";
            if (userNode.TryGetProperty("user_metadata", out var meta) && meta.TryGetProperty("username", out var uname))
            {
                username = uname.GetString() ?? username;
            }

            return new AuthSessionData(
                tokenProp.GetString() ?? string.Empty,
                refreshProp.GetString() ?? string.Empty,
                userNode.GetProperty("id").GetString() ?? string.Empty,
                username,
                DateTime.Now.AddSeconds(expiresIn));
        }
        catch (Exception) { return null; }
    }

    private static string ExtractErrorMessage(string? json)
    {
        if (json is null) return "Couldn't reach the server. Check your internet connection.";

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var key in new[] { "error_description", "msg", "message" })
            {
                if (doc.RootElement.TryGetProperty(key, out var prop) && prop.GetString() is { Length: > 0 } text) return text;
            }
        }
        catch { /* Ignore parsing errors */ }
        return "Something went wrong. Please try again.";
    }

    // --- DATABASE / STORAGE METHODS ---

    // --- Upload ---

    /// <summary>
    /// A fresh, unguessable file name. The bucket is public, so a private deck's file stays
    /// out of reach only while nobody can work out its name.
    /// </summary>
    private static string NewStoragePath(string userId) => $"{userId}/{Guid.NewGuid():N}.json";

    public async Task<(bool Success, string Message)> UploadCloudDeckAsync(string userId, string title, string description, int cardCount, string jsonPayload, bool isPrivate)
    {
        string fileName = NewStoragePath(userId);

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
                description,
                storage_path = fileName,
                card_count = cardCount,
                visibility = isPrivate ? PrivateVisibility : PublicVisibility
            };

            var dbContent = new StringContent(JsonSerializer.Serialize(dbPayload), Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.Add("Prefer", "return=minimal");

            using var dbRes = await _http.PostAsync(dbUrl, dbContent).ConfigureAwait(false);
            _http.DefaultRequestHeaders.Remove("Prefer");

            if (dbRes.IsSuccessStatusCode) return (true, isPrivate ? "Deck uploaded privately!" : "Deck uploaded successfully!");

            return (false, $"Database Error: {dbRes.StatusCode} - {await dbRes.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex) { return (false, $"Network error: {ex.Message}"); }
    }

    public async Task<(bool Success, string Message)> UpdateCloudDeckAsync(string storagePath, string title, string description, int cardCount, string jsonPayload)
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
                description,
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

    /// <summary>
    /// Lists or unlists one of the user's decks. Going private also renames the file: its old
    /// name was visible to everyone while the deck was public.
    /// </summary>
    public async Task<(bool Success, string Message)> SetCloudDeckVisibilityAsync(string userId, string storagePath, bool makePrivate)
    {
        try
        {
            string newPath = makePrivate ? NewStoragePath(userId) : storagePath;

            if (makePrivate)
            {
                var (moved, moveError) = await MoveStorageFileAsync(storagePath, newPath).ConfigureAwait(false);
                if (!moved) return (false, $"Storage Error: {moveError}");
            }

            string dbUrl = $"{ProjectURL}/rest/v1/decks?storage_path=eq.{Uri.EscapeDataString(storagePath)}";
            var dbPayload = new
            {
                visibility = makePrivate ? PrivateVisibility : PublicVisibility,
                storage_path = newPath
            };
            var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), dbUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(dbPayload), Encoding.UTF8, "application/json")
            };

            using var dbRes = await _http.SendAsync(patchReq).ConfigureAwait(false);
            if (!dbRes.IsSuccessStatusCode)
            {
                // Put the file back so the row still points at it.
                if (makePrivate) await MoveStorageFileAsync(newPath, storagePath).ConfigureAwait(false);
                return (false, $"Database Error: {dbRes.StatusCode} - {await dbRes.Content.ReadAsStringAsync()}");
            }

            return (true, makePrivate ? "Deck is now private." : "Deck is now public.");
        }
        catch (Exception ex) { return (false, $"Network error: {ex.Message}"); }
    }

    private async Task<(bool Success, string? Error)> MoveStorageFileAsync(string fromPath, string toPath)
    {
        var payload = new { bucketId = "decks", sourceKey = fromPath, destinationKey = toPath };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var res = await _http.PostAsync($"{ProjectURL}/storage/v1/object/move", content).ConfigureAwait(false);
        return res.IsSuccessStatusCode ? (true, null) : (false, $"{res.StatusCode} - {await res.Content.ReadAsStringAsync()}");
    }

    public async Task<(bool Success, string Message)> DeleteCloudDeckAsync(string storagePath)
    {
        try
        {
            // Row first: if the file delete then fails, the deck is merely orphaned in storage
            // rather than listed publicly with nothing behind it.
            string dbUrl = $"{ProjectURL}/rest/v1/decks?storage_path=eq.{Uri.EscapeDataString(storagePath)}";
            using var dbRes = await _http.DeleteAsync(dbUrl).ConfigureAwait(false);

            if (!dbRes.IsSuccessStatusCode)
                return (false, $"Database delete failed: {await dbRes.Content.ReadAsStringAsync()}");

            string storageUrl = $"{ProjectURL}/storage/v1/object/decks/{storagePath}";
            using var storageRes = await _http.DeleteAsync(storageUrl).ConfigureAwait(false);

            if (!storageRes.IsSuccessStatusCode)
            {
                var storageErr = await storageRes.Content.ReadAsStringAsync();
                return (false, $"Storage Delete Error: {storageRes.StatusCode} - {storageErr}");
            }

            return (true, "Deck deleted successfully.");
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

    /// <summary>
    /// Counts a download for the signed-in user (once per user, never the owner).
    /// Returns the deck's new total, or null if it couldn't be recorded.
    /// </summary>
    public async Task<int?> RecordDownloadAsync(Guid deckId)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(new { p_deck_id = deckId }), Encoding.UTF8, "application/json");
            using var res = await _http.PostAsync($"{ProjectURL}/rest/v1/rpc/record_deck_download", content).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                Logger.LogInfo($"Recording download failed: {res.StatusCode} - {json}");
                return null;
            }

            return int.TryParse(json, out var count) ? count : null;
        }
        catch (Exception ex)
        {
            Logger.LogInfo($"Recording download failed: {ex.Message}");
            return null;
        }
    }

    public async Task<List<FlashCardDeckMetadata>> GetUserCloudDecksAsync(string userId)
    {
        var url = $"{ProjectURL}/rest/v1/decks?select=id,owner_id,title,description,storage_path,card_count,download_count,visibility,version,created_at,updated_at&storage_path=ilike.{userId}/*";

        using var res = await _http.GetAsync(url).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return [];

        return JsonSerializer.Deserialize<List<FlashCardDeckMetadata>>(
            await res.Content.ReadAsStringAsync().ConfigureAwait(false), TextUtility.CaseInsensitive) ?? [];
    }

    public async Task<List<FlashCardDeckMetadata>> GetPublicDecksAsync(string searchText = "", int limit = 25)
    {
        // search_public_decks matches the title or the uploader's name and returns the most
        // downloaded first, capped at p_limit (see Supabase/1.1_search_by_uploader.sql).
        var url = $"{ProjectURL}/rest/v1/rpc/search_public_decks?select=id,owner_id,title,description,storage_path,card_count,download_count,version,created_at,updated_at,owner:profiles(display_name)&p_limit={limit}";
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            url += $"&p_query={Uri.EscapeDataString(searchText.Trim())}";
        }

        using var res = await _http.GetAsync(url).ConfigureAwait(false);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"Database Error ({res.StatusCode}): {await res.Content.ReadAsStringAsync()}");

        return JsonSerializer.Deserialize<List<FlashCardDeckMetadata>>(
            await res.Content.ReadAsStringAsync().ConfigureAwait(false), TextUtility.CaseInsensitive) ?? [];
    }
}

/// <summary> A signed-in Supabase session as the app stores it. </summary>
public sealed record AuthSessionData(string AccessToken, string RefreshToken, string UserId, string Username, DateTime Expiration);

/// <summary> Outcome of an auth call: a message for the user, plus the session when one was issued. </summary>
public sealed record AuthResult(bool Success, string Message, AuthSessionData? Session = null, bool Rejected = false)
{
    public static AuthResult Ok(string message, AuthSessionData? session = null) => new(true, message, session);
    public static AuthResult Fail(string message, bool rejected = false) => new(false, message, null, rejected);
}
