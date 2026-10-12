using System;
using System.Threading.Tasks;
using ReviFlash.Data.Local;

namespace ReviFlash.Data.Online;

/// <summary> Whether a stored session is usable: Offline means the server couldn't be reached, so the session is kept. </summary>
public enum SessionState
{
    Active,
    SignedOut,
    Offline,
}

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

    /// <summary> Restores the session, refreshing it first if needed; only a server rejection clears it. </summary>
    public static Task<SessionState> TryRestoreAsync()
    {
        if (HasValidAccessToken) return Task.FromResult(SessionState.Active);

        // Concurrent callers share one refresh: a spent refresh token can make Supabase revoke the session (UI thread only, no lock).
        return _refreshing ??= RefreshAsync();
    }

    private static Task<SessionState>? _refreshing;

    private static async Task<SessionState> RefreshAsync()
    {
        // Yield first so _refreshing is assigned before the finally below can clear it.
        await Task.Yield();
        try { return await RefreshCoreAsync(); }
        finally { _refreshing = null; }
    }

    private static async Task<SessionState> RefreshCoreAsync()
    {
        var refreshToken = MetaDataManager.Data.SupabaseRefreshToken;
        if (string.IsNullOrEmpty(refreshToken)) return SessionState.SignedOut;

        using var client = new SupabaseConnection();
        var result = await client.RefreshSessionAsync(refreshToken);

        if (result.Session is not null)
        {
            Store(result.Session);
            return SessionState.Active;
        }

        if (!result.Rejected) return SessionState.Offline;

        Clear();
        return SessionState.SignedOut;
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

    /// <summary> Revokes this device's session on the server (best effort, refreshing a lapsed token first) and forgets it locally. </summary>
    public static async Task SignOutAsync()
    {
        try
        {
            if (await TryRestoreAsync() == SessionState.Active)
            {
                using var client = new SupabaseConnection();
                await client.SignOutAsync();
            }
        }
        catch (Exception ex) { Logger.LogError("Sign-out could not reach the server", ex); }

        Clear();
    }

    private static void Clear()
    {
        MetaDataManager.Data.SetSupabase(null, null, null, null, DateTime.MinValue);
        MetaDataManager.SaveMetaData();
    }
}
