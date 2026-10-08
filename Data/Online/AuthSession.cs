using System;
using System.Threading.Tasks;
using ReviFlash.Data.Local;

namespace ReviFlash.Data.Online;

/// <summary> Tracks the ReviFlash Online sign-in; sessions are stored and quietly refreshed so they last until sign-out. </summary>
public static class AuthSession
{
    // Refresh a little early so a token never expires halfway through a request.
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    public static string? UserId => MetaDataManager.Data.SupabaseUserId;
    public static string Username => MetaDataManager.Data.SupabaseUsername ?? "User";

    /// <summary> A session is stored, even if lapsed: it is refreshed on next use and cleared only if the server rejects it. </summary>
    public static bool IsSignedIn =>
        HasValidAccessToken || !string.IsNullOrEmpty(MetaDataManager.Data.SupabaseRefreshToken);

    public static bool HasValidAccessToken =>
        !string.IsNullOrEmpty(MetaDataManager.Data.SupabaseAccessToken) &&
        // Margin added to now, not taken off the expiry: signed out, that is DateTime.MinValue.
        DateTime.Now + ExpiryMargin < MetaDataManager.Data.SupabaseExpirationTime;

    /// <summary> True when there is a usable session, refreshing it first if needed; only a server rejection clears it. </summary>
    public static Task<bool> TryRestoreAsync()
    {
        if (HasValidAccessToken) return Task.FromResult(true);

        // Concurrent callers share one refresh: a spent refresh token can make Supabase revoke the session (UI thread only, no lock).
        return _refreshing ??= RefreshAsync();
    }

    private static Task<bool>? _refreshing;

    private static async Task<bool> RefreshAsync()
    {
        // Yield first so _refreshing is assigned before the finally below can clear it.
        await Task.Yield();
        try { return await RefreshCoreAsync(); }
        finally { _refreshing = null; }
    }

    private static async Task<bool> RefreshCoreAsync()
    {
        var refreshToken = MetaDataManager.Data.SupabaseRefreshToken;
        if (string.IsNullOrEmpty(refreshToken)) return false;

        using var client = new SupabaseConnection();
        var result = await client.RefreshSessionAsync(refreshToken);

        if (result.Session is not null)
        {
            Store(result.Session);
            return true;
        }

        if (result.Rejected) Clear();
        return false;
    }

    public static void Store(AuthSessionData session)
    {
        MetaDataManager.Data.SetSupabase(session.AccessToken, session.RefreshToken, session.UserId, session.Username, session.Expiration);
        MetaDataManager.SaveMetaData();
    }

    public static async Task<AuthResult> ChangeUsernameAsync(string username)
    {
        if (UserId is not { Length: > 0 } userId) return AuthResult.Fail("You're not signed in.");

        using var client = await SupabaseConnection.CreateAsync();
        var result = await client.UpdateUsernameAsync(userId, username);
        if (!result.Success) return result;

        MetaDataManager.Data.SupabaseUsername = username;
        MetaDataManager.SaveMetaData();
        return result;
    }

    /// <summary> Revokes the session on the server (best effort) and forgets it locally. </summary>
    public static async Task SignOutAsync()
    {
        if (HasValidAccessToken)
        {
            using var client = new SupabaseConnection();
            await client.SignOutAsync();
        }

        Clear();
    }

    private static void Clear()
    {
        MetaDataManager.Data.SetSupabase(null, null, null, null, DateTime.MinValue);
        MetaDataManager.SaveMetaData();
    }
}
