using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Data.Online;

namespace ReviFlash.ViewModels;

public enum LoginMode
{
    SignIn,
    SignUp,
    ForgotPassword,
    ResetPassword,
}

/// <summary> ReviFlash Online sign-in, sign-up and emailed-code password reset. </summary>
public partial class LoginViewModel : ViewModelBase
{
    // Supabase's default minimum; the server is the real authority.
    private const int MinPasswordLength = 6;
    // Also enforced by the profiles_display_name_length constraint on the server.
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 24;

    [NotifyPropertyChangedFor(nameof(Title), nameof(Subtitle), nameof(PrimaryActionText))]
    [NotifyPropertyChangedFor(nameof(IsSignIn), nameof(IsSignUp), nameof(IsRecovering), nameof(IsResetting))]
    [NotifyPropertyChangedFor(nameof(ShowTabs), nameof(ShowEmail), nameof(ShowPassword), nameof(ShowConfirmPassword))]
    [NotifyPropertyChangedFor(nameof(PasswordLabel))]
    [ObservableProperty] private LoginMode _mode = LoginMode.SignIn;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string _resetCode = string.Empty;

    [NotifyPropertyChangedFor(nameof(HasStatus))]
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isStatusError;

    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    [ObservableProperty] private bool _isBusy;

    public bool HasStatus => StatusMessage.Length > 0;

    public bool IsSignIn => Mode == LoginMode.SignIn;
    public bool IsSignUp => Mode == LoginMode.SignUp;
    public bool IsResetting => Mode == LoginMode.ResetPassword;

    /// <summary> Either step of the forgotten-password flow. </summary>
    public bool IsRecovering => Mode is LoginMode.ForgotPassword or LoginMode.ResetPassword;

    public bool ShowTabs => !IsRecovering;
    public bool ShowEmail => Mode != LoginMode.ResetPassword;
    public bool ShowPassword => Mode != LoginMode.ForgotPassword;
    public bool ShowConfirmPassword => Mode is LoginMode.SignUp or LoginMode.ResetPassword;

    public string PasswordLabel => IsResetting ? "New password" : "Password";

    public string Title => Mode switch
    {
        LoginMode.SignUp => "Create your account",
        LoginMode.ForgotPassword => "Forgot your password?",
        LoginMode.ResetPassword => "Choose a new password",
        _ => "Welcome back",
    };

    public string Subtitle => Mode switch
    {
        LoginMode.SignUp => "Share your sets and download ones made by others.",
        LoginMode.ForgotPassword => "Enter your account's email and we'll send you a reset code.",
        LoginMode.ResetPassword => $"Enter the code we sent to {Email.Trim()}.",
        _ => "Sign in to ReviFlash Online to share and download sets.",
    };

    public string PrimaryActionText => Mode switch
    {
        LoginMode.SignUp => "Create Account",
        LoginMode.ForgotPassword => "Send Reset Code",
        LoginMode.ResetPassword => "Reset Password",
        _ => "Sign In",
    };

    /// <summary> Raised once a session has been stored, so the window can close. </summary>
    public event Action? SignedIn;

    // --- Mode switching ---

    [RelayCommand] private void ShowSignIn() => SwitchTo(LoginMode.SignIn);
    [RelayCommand] private void ShowSignUp() => SwitchTo(LoginMode.SignUp);
    [RelayCommand] private void ShowForgotPassword() => SwitchTo(LoginMode.ForgotPassword);

    private void SwitchTo(LoginMode mode, string? status = null, bool isError = false)
    {
        Mode = mode;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        if (mode != LoginMode.ResetPassword) ResetCode = string.Empty;
        SetStatus(status ?? string.Empty, isError);
    }

    // --- Submit ---

    private bool CanSubmit() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        if (Validate() is string problem)
        {
            SetStatus(problem, isError: true);
            return;
        }

        IsBusy = true;
        SetStatus(Mode switch
        {
            LoginMode.SignUp => "Creating your account...",
            LoginMode.ForgotPassword => "Sending reset code...",
            LoginMode.ResetPassword => "Resetting password...",
            _ => "Signing in...",
        });

        try
        {
            switch (Mode)
            {
                case LoginMode.SignIn: await SignInAsync(); break;
                case LoginMode.SignUp: await SignUpAsync(); break;
                case LoginMode.ForgotPassword: await SendResetCodeAsync(); break;
                case LoginMode.ResetPassword: await ResetPasswordAsync(); break;
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResendCodeAsync()
    {
        IsBusy = true;
        try { await SendResetCodeAsync(); }
        finally { IsBusy = false; }
    }

    private async Task SignInAsync()
    {
        using var client = new SupabaseConnection();
        var result = await client.SignInAsync(Email.Trim(), Password);
        Complete(result);
    }

    private async Task SignUpAsync()
    {
        using var client = new SupabaseConnection();
        var result = await client.SignUpAsync(Email.Trim(), Password, Username.Trim());

        if (!result.Success || result.Session is not null)
        {
            Complete(result);
            return;
        }

        // Email confirmation is on: nothing to store yet, so send them to sign in afterwards.
        SwitchTo(LoginMode.SignIn, result.Message);
    }

    private async Task SendResetCodeAsync()
    {
        using var client = new SupabaseConnection();
        var result = await client.RequestPasswordResetAsync(Email.Trim());

        if (result.Success) SwitchTo(LoginMode.ResetPassword, result.Message);
        else SetStatus(result.Message, isError: true);
    }

    private async Task ResetPasswordAsync()
    {
        AuthResult verified;
        using (var client = new SupabaseConnection())
        {
            verified = await client.VerifyRecoveryCodeAsync(Email.Trim(), ResetCode.Trim());
        }

        if (verified.Session is null)
        {
            SetStatus(verified.Message, isError: true);
            return;
        }

        // The recovery session is what authorises the password change.
        AuthSession.Store(verified.Session);

        using var authed = new SupabaseConnection();
        var updated = await authed.UpdatePasswordAsync(Password);

        if (!updated.Success)
        {
            SetStatus(updated.Message, isError: true);
            return;
        }

        SignedIn?.Invoke();
    }

    private void Complete(AuthResult result)
    {
        if (result.Session is null)
        {
            SetStatus(result.Message, isError: true);
            return;
        }

        AuthSession.Store(result.Session);
        Password = string.Empty;
        SignedIn?.Invoke();
    }

    // --- Validation ---

    /// <summary> The first thing wrong with the form, or null when it can be sent. </summary>
    private string? Validate()
    {
        if (ShowEmail && !LooksLikeEmail(Email)) return "Enter a valid email address.";

        if (IsSignUp)
        {
            int length = Username.Trim().Length;
            if (length < MinUsernameLength || length > MaxUsernameLength)
                return $"Usernames must be {MinUsernameLength}-{MaxUsernameLength} characters.";
        }

        if (IsResetting && string.IsNullOrWhiteSpace(ResetCode)) return "Enter the code from the email.";

        if (ShowPassword)
        {
            if (string.IsNullOrEmpty(Password)) return "Enter your password.";
            if (ShowConfirmPassword && Password.Length < MinPasswordLength)
                return $"Passwords must be at least {MinPasswordLength} characters.";
            if (ShowConfirmPassword && Password != ConfirmPassword) return "The passwords don't match.";
        }

        return null;
    }

    private static bool LooksLikeEmail(string email)
    {
        var trimmed = email.Trim();
        int at = trimmed.IndexOf('@');
        return at > 0 && at < trimmed.Length - 1 && trimmed.IndexOf('.', at) > at + 1 && !trimmed.Contains(' ');
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
