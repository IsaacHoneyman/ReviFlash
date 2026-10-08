using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ReviFlash.Utilities;
using ReviFlash.Data.Local;

namespace ReviFlash.Data.Online;

public sealed class SupabaseConnection : IDisposable
{
    private const string ProjectURL = "https://hegjwggsueldwtnxpnnv.supabase.co";
    private const string AnonKey = "sb_publishable_XETpGgIYnJHFwrq28EV-4w_-aZ_UdV4";

    public const string PublicVisibility = "public";
    public const string PrivateVisibility = "private";

    private readonly HttpClient _http;

    public SupabaseConnection()
    {
        // An expired session token gets a 401 even for public reads, so fall back to the anon key.
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

    /// <summary> A connection carrying the current session, refreshed first if lapsed; auth calls work with the anon key. </summary>
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
        var (ok, _, json) = await PostAuthAsync("signup", payload).ConfigureAwait(false);
        if (!ok) return AuthResult.Fail(ExtractErrorMessage(json));

        var session = TryParseSession(json);
        return session is null
            ? AuthResult.Ok("Account created! Check your email to confirm it, then sign in.")
            : AuthResult.Ok("Account created!", session);
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        var (ok, _, json) = await PostAuthAsync("token?grant_type=password", new { email, password }).ConfigureAwait(false);
        if (!ok) return AuthResult.Fail(ExtractErrorMessage(json));

        var session = TryParseSession(json);
        return session is null ? AuthResult.Fail("Unexpected response from the server.") : AuthResult.Ok("Signed in.", session);
    }

    /// <summary> Swaps a refresh token for a fresh session. <see cref="AuthResult.Rejected"/> means sign in again. </summary>
    public async Task<AuthResult> RefreshSessionAsync(string refreshToken)
    {
        var (ok, status, json) = await PostAuthAsync("token?grant_type=refresh_token", new { refresh_token = refreshToken }).ConfigureAwait(false);
        // Only a 4xx means the token is dead; rate limits and server errors keep the session for a retry.
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
        var (ok, _, json) = await PostAuthAsync("recover", new { email }).ConfigureAwait(false);
        return ok
            ? AuthResult.Ok($"If an account exists for {email}, a reset code is on its way.")
            : AuthResult.Fail(ExtractErrorMessage(json));
    }

    /// <summary> Trades the emailed recovery code for a session that may set a new password. </summary>
    public async Task<AuthResult> VerifyRecoveryCodeAsync(string email, string code)
    {
        var (ok, _, json) = await PostAuthAsync("verify", new { type = "recovery", email, token = code }).ConfigureAwait(false);
        if (!ok) return AuthResult.Fail(ExtractErrorMessage(json));

        var session = TryParseSession(json);
        return session is null ? AuthResult.Fail("Unexpected response from the server.") : AuthResult.Ok("Code accepted.", session);
    }

    /// <summary> Sets a new password for the signed-in user (this connection's session). </summary>
    public async Task<AuthResult> UpdatePasswordAsync(string newPassword)
    {
        var res = await SendAsync(HttpMethod.Put, "auth/v1/user", new { password = newPassword }).ConfigureAwait(false);
        return res.Ok ? AuthResult.Ok("Password updated.") : AuthResult.Fail(ExtractErrorMessage(res.Body));
    }

    /// <summary> Renames the user in both their profile (shown on community decks) and auth metadata (read at sign-in). </summary>
    public async Task<AuthResult> UpdateUsernameAsync(string userId, string username)
    {
        var profileRes = await SendAsync(HttpMethod.Patch, $"rest/v1/profiles?id=eq.{Uri.EscapeDataString(userId)}", new { display_name = username }).ConfigureAwait(false);
        if (!profileRes.Ok) return AuthResult.Fail(ExtractErrorMessage(profileRes.Body));

        var userRes = await SendAsync(HttpMethod.Put, "auth/v1/user", new { data = new { username } }).ConfigureAwait(false);
        return userRes.Ok ? AuthResult.Ok("Username updated.") : AuthResult.Fail(ExtractErrorMessage(userRes.Body));
    }

    /// <summary> Revokes this connection's session on the server. Best effort: failures are only logged. </summary>
    public async Task SignOutAsync()
    {
        var res = await SendAsync(HttpMethod.Post, "auth/v1/logout").ConfigureAwait(false);
        if (!res.Ok && res.Body is not null) Logger.LogInfo(ErrorText("Supabase logout failed", res));
    }

    private Task<Reply> PostAuthAsync(string endpoint, object payload) => SendAsync(HttpMethod.Post, $"auth/v1/{endpoint}", payload);

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

    // --- HTTP ---

    /// <summary> A server reply. Body is null (and Status 0) only when the request never reached the server. </summary>
    private sealed record Reply(bool Ok, int Status, string? Body);

    private async Task<Reply> SendAsync(HttpMethod method, string path, object? json = null, HttpContent? content = null, string? prefer = null)
    {
        try
        {
            using var req = new HttpRequestMessage(method, $"{ProjectURL}/{path}")
            {
                Content = content ?? (json is null ? null : new StringContent(JsonSerializer.Serialize(json), Encoding.UTF8, "application/json"))
            };
            if (prefer is not null) req.Headers.Add("Prefer", prefer);

            using var res = await _http.SendAsync(req).ConfigureAwait(false);
            return new Reply(res.IsSuccessStatusCode, (int)res.StatusCode, await res.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            Logger.LogInfo($"Supabase {method} '{path}' failed: {ex.Message}");
            return new Reply(false, 0, null);
        }
    }

    private static string ErrorText(string label, Reply reply) => reply.Body is null
        ? ExtractErrorMessage(null)
        : $"{label}: {(HttpStatusCode)reply.Status} - {ExtractErrorMessage(reply.Body)}";

    private const string DeckColumns = "id,owner_id,title,description,storage_path,card_count,download_count,version,created_at,updated_at";

    private static string EscapeKey(string key) => string.Join('/', key.Split('/').Select(s => Uri.EscapeDataString(s)));

    private static string StorageObject(string storagePath) => $"storage/v1/object/decks/{EscapeKey(storagePath)}";

    private static string DeckRow(string storagePath) => $"rest/v1/decks?storage_path=eq.{Uri.EscapeDataString(storagePath)}";

    /// <summary> The deck file body, typed application/json without a charset. </summary>
    private static StringContent DeckFileContent(string jsonPayload)
    {
        var content = new StringContent(jsonPayload, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    // --- DATABASE / STORAGE METHODS ---

    // --- Upload ---

    /// <summary> A fresh, unguessable file name: the bucket is public, so a private deck is hidden only by its name. </summary>
    private static string NewStoragePath(string userId) => $"{userId}/{Guid.NewGuid():N}.json";

    public async Task<(bool Success, string Message)> UploadCloudDeckAsync(string userId, string title, string description, int cardCount, string jsonPayload, bool isPrivate)
    {
        string fileName = NewStoragePath(userId);

        var storageRes = await SendAsync(HttpMethod.Post, StorageObject(fileName), content: DeckFileContent(jsonPayload)).ConfigureAwait(false);
        if (!storageRes.Ok) return (false, ErrorText("Storage Error", storageRes));

        var dbPayload = new
        {
            title,
            description,
            storage_path = fileName,
            card_count = cardCount,
            visibility = isPrivate ? PrivateVisibility : PublicVisibility
        };

        var dbRes = await SendAsync(HttpMethod.Post, "rest/v1/decks", dbPayload, prefer: "return=minimal").ConfigureAwait(false);
        if (dbRes.Ok) return (true, isPrivate ? "Deck uploaded privately!" : "Deck uploaded successfully!");

        return (false, ErrorText("Database Error", dbRes));
    }

    public async Task<(bool Success, string Message)> UpdateCloudDeckAsync(string storagePath, string title, string description, int cardCount, string jsonPayload)
    {
        var storageRes = await SendAsync(HttpMethod.Put, StorageObject(storagePath), content: DeckFileContent(jsonPayload)).ConfigureAwait(false);
        if (!storageRes.Ok) return (false, ErrorText("Storage Update Error", storageRes));

        var dbPayload = new
        {
            title,
            description,
            card_count = cardCount,
            updated_at = DateTimeOffset.UtcNow.ToString("o")
        };

        var dbRes = await SendAsync(HttpMethod.Patch, DeckRow(storagePath), dbPayload).ConfigureAwait(false);
        if (dbRes.Ok) return (true, "Deck updated successfully!");

        return (false, ErrorText("Database Update Error", dbRes));
    }

    /// <summary> Lists or unlists a deck; going private also renames the file, since its public name was visible to everyone. </summary>
    public async Task<(bool Success, string Message)> SetCloudDeckVisibilityAsync(string userId, string storagePath, bool makePrivate)
    {
        string newPath = makePrivate ? NewStoragePath(userId) : storagePath;

        if (makePrivate)
        {
            var moveRes = await MoveStorageFileAsync(storagePath, newPath).ConfigureAwait(false);
            if (!moveRes.Ok) return (false, ErrorText("Storage Error", moveRes));
        }

        var dbPayload = new
        {
            visibility = makePrivate ? PrivateVisibility : PublicVisibility,
            storage_path = newPath
        };

        var dbRes = await SendAsync(HttpMethod.Patch, DeckRow(storagePath), dbPayload).ConfigureAwait(false);
        if (!dbRes.Ok)
        {
            // Put the file back so the row still points at it.
            if (makePrivate) await MoveStorageFileAsync(newPath, storagePath).ConfigureAwait(false);
            return (false, ErrorText("Database Error", dbRes));
        }

        return (true, makePrivate ? "Deck is now private." : "Deck is now public.");
    }

    private Task<Reply> MoveStorageFileAsync(string fromPath, string toPath) =>
        SendAsync(HttpMethod.Post, "storage/v1/object/move", new { bucketId = "decks", sourceKey = fromPath, destinationKey = toPath });

    public async Task<(bool Success, string Message)> DeleteCloudDeckAsync(string storagePath)
    {
        // Row first: a failed file delete then leaves an orphaned file, not a listing with nothing behind it.
        var dbRes = await SendAsync(HttpMethod.Delete, DeckRow(storagePath)).ConfigureAwait(false);
        if (!dbRes.Ok) return (false, ErrorText("Database Delete Error", dbRes));

        var storageRes = await SendAsync(HttpMethod.Delete, StorageObject(storagePath)).ConfigureAwait(false);
        if (!storageRes.Ok) return (false, ErrorText("Storage Delete Error", storageRes));

        return (true, "Deck deleted successfully.");
    }

    // --- Find --- 

    public async Task<string> DownloadCloudDeckJsonAsync(string storagePath, string bucket = "decks")
    {
        if (string.IsNullOrWhiteSpace(storagePath)) throw new ArgumentNullException(nameof(storagePath));
        var key = storagePath.StartsWith(bucket + "/", StringComparison.OrdinalIgnoreCase)
            ? storagePath[(bucket.Length + 1)..]
            : storagePath;

        var res = await SendAsync(HttpMethod.Get, $"storage/v1/object/public/{bucket}/{EscapeKey(key)}").ConfigureAwait(false);
        if (!res.Ok) throw new HttpRequestException(ErrorText("Download Error", res));
        return res.Body!;
    }

    /// <summary> Counts a download once per signed-in user (never the owner); returns the new total, or null on failure. </summary>
    public async Task<int?> RecordDownloadAsync(Guid deckId)
    {
        var res = await SendAsync(HttpMethod.Post, "rest/v1/rpc/record_deck_download", new { p_deck_id = deckId }).ConfigureAwait(false);
        if (!res.Ok)
        {
            if (res.Body is not null) Logger.LogInfo(ErrorText("Recording download failed", res));
            return null;
        }

        return int.TryParse(res.Body, out var count) ? count : null;
    }

    public async Task<List<FlashCardDeckMetadata>> GetUserCloudDecksAsync(string userId)
    {
        var res = await SendAsync(HttpMethod.Get, $"rest/v1/decks?select={DeckColumns},visibility&storage_path=ilike.{Uri.EscapeDataString(userId)}/*").ConfigureAwait(false);
        if (res.Body is null) throw new HttpRequestException(ExtractErrorMessage(null));
        if (!res.Ok) return [];

        return JsonSerializer.Deserialize<List<FlashCardDeckMetadata>>(res.Body, TextUtility.CaseInsensitive) ?? [];
    }

    public async Task<List<FlashCardDeckMetadata>> GetPublicDecksAsync(string searchText = "", int limit = 25)
    {
        // search_public_decks (a Supabase SQL function) matches title or uploader name, most downloaded first.
        var path = $"rest/v1/rpc/search_public_decks?select={DeckColumns},owner:profiles(display_name)&p_limit={limit}";
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            path += $"&p_query={Uri.EscapeDataString(searchText.Trim())}";
        }

        var res = await SendAsync(HttpMethod.Get, path).ConfigureAwait(false);
        if (!res.Ok) throw new HttpRequestException(ErrorText("Database Error", res));

        return JsonSerializer.Deserialize<List<FlashCardDeckMetadata>>(res.Body!, TextUtility.CaseInsensitive) ?? [];
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
