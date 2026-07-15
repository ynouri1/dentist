using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ortho.Application.Patients;
using Ortho.Application.Users;
using Ortho.Domain.Entities;
using Ortho.UI.Localization;

namespace Ortho.UI.ViewModels;

public enum LoginMode
{
    /// <summary>Premier lancement : création du compte praticien initial.</summary>
    FirstRun,
    Login,
    /// <summary>Session verrouillée : seul l'utilisateur courant peut déverrouiller.</summary>
    Unlock,
}

public partial class LoginViewModel(UserService users) : ViewModelBase
{
    [ObservableProperty] private LoginMode _mode = LoginMode.Login;
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _passwordConfirmation = "";
    [ObservableProperty] private int _roleIndex;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _busy;

    /// <summary>Invoqué avec l'utilisateur authentifié.</summary>
    public Action<AppUser>? Succeeded { get; set; }

    /// <summary>Montre le code de secours au praticien (attendu avant de continuer).</summary>
    public Func<string, Task>? RecoveryCodeGenerated { get; set; }

    public bool IsFirstRun => Mode == LoginMode.FirstRun;
    public bool IsUnlock => Mode == LoginMode.Unlock;

    /// <summary>Mot de passe oublié / restauration : uniquement sur l'écran de connexion.</summary>
    public bool ShowRecoveryActions => Mode == LoginMode.Login;

    public string Title => Mode switch
    {
        LoginMode.FirstRun => L.LoginTitleFirstRun,
        LoginMode.Unlock => L.LoginTitleUnlock,
        _ => L.LoginTitleDefault,
    };

    public string SubmitLabel => Mode switch
    {
        LoginMode.FirstRun => L.LoginSubmitFirstRun,
        LoginMode.Unlock => L.LoginSubmitUnlock,
        _ => L.LoginSubmitDefault,
    };

    partial void OnModeChanged(LoginMode value)
    {
        OnPropertyChanged(nameof(IsFirstRun));
        OnPropertyChanged(nameof(IsUnlock));
        OnPropertyChanged(nameof(ShowRecoveryActions));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SubmitLabel));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Busy)
            return;

        Busy = true;
        Error = null;
        try
        {
            if (Mode == LoginMode.FirstRun)
            {
                if (Password != PasswordConfirmation)
                {
                    Error = L.ErrorPasswordMismatch;
                    return;
                }
                var creation = await users.CreateAsync(Username, DisplayName, Password, (UserRole)RoleIndex);
                if (RecoveryCodeGenerated is { } showCode)
                    await showCode(creation.RecoveryCode);
            }

            var user = await users.AuthenticateAsync(Username, Password);
            if (user is null)
            {
                Error = L.ErrorBadCredentials;
                return;
            }

            Password = "";
            PasswordConfirmation = "";
            Succeeded?.Invoke(user);
        }
        catch (ValidationException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Error = L.F("ErrorUnexpected", ex.Message);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Réinitialisation par code de secours ; retourne le nouveau code ou null.</summary>
    public async Task<string?> ResetPasswordAsync(string username, string recoveryCode, string newPassword)
    {
        try
        {
            Error = null;
            var newCode = await users.ResetPasswordAsync(username, recoveryCode, newPassword);
            if (newCode is null)
                Error = L.Get("RecoveryFailed");
            return newCode;
        }
        catch (ValidationException ex)
        {
            Error = ex.Message;
            return null;
        }
    }
}
