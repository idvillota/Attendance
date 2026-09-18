using Attendance.Core.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _auth;

    public LoginViewModel(IAuthService auth)
    {
        _auth = auth;
        NeedsSetup = !auth.HasUsers();
        Title = NeedsSetup ? "Crear administrador" : "Iniciar sesión";
        Hint = NeedsSetup
            ? "Es la primera vez. Cree el usuario administrador de este restaurante. No lo pierda: no hay recuperación en la nube."
            : "Entre con su usuario. El administrador ve toda la nómina; una cuenta básica solo ve sus propias horas.";
    }

    public bool NeedsSetup { get; }
    public string Title { get; }
    public string Hint { get; }

    public event Action? Succeeded;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string _error = string.Empty;

    [RelayCommand]
    private void Submit()
    {
        try
        {
            Error = string.Empty;
            if (NeedsSetup)
            {
                if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
                {
                    Error = "Las contraseñas no coinciden.";
                    return;
                }

                _auth.CreateAdmin(Username, Password);
            }
            else
            {
                _auth.SignIn(Username, Password);
            }

            Succeeded?.Invoke();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
