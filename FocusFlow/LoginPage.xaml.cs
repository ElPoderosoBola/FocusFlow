using FocusFlow.Models;
using FocusFlow.Services;

namespace FocusFlow;

public partial class LoginPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly SoundService _soundService;

    public LoginPage(DatabaseService databaseService, SoundService soundService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _soundService = soundService;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await _soundService.PlayClickAsync();

        var username = UsernameEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Login", "Debes escribir usuario y contraseña.", "OK");
            return;
        }

        var user = await _databaseService.LoginUserAsync(username, password);
        if (user is null)
        {
            await _soundService.PlayFailAsync();
            await DisplayAlert("Login", "Usuario o contraseña incorrectos.", "OK");
            return;
        }

        var session = await _databaseService.GetUserSessionAsync();
        session.CurrentUserId = user.Id;
        session.LastAccessDate = DateTime.Today;
        await _databaseService.SaveUserSessionAsync(session);

        await _soundService.PlaySuccessAsync();
        Application.Current.Windows[0].Page = new AppShell();
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        await _soundService.PlayClickAsync();

        var username = UsernameEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Registro", "Debes escribir usuario y contraseña.", "OK");
            return;
        }

        var newUser = new User
        {
            Username = username,
            Password = password
        };

        var registered = await _databaseService.RegisterUserAsync(newUser);
        if (!registered)
        {
            await _soundService.PlayFailAsync();
            await DisplayAlert("Registro", "Ese nombre de usuario ya existe.", "OK");
            return;
        }

        var savedUser = await _databaseService.LoginUserAsync(username, password);
        if (savedUser is null)
        {
            await DisplayAlert("Registro", "Error al iniciar con el usuario recién creado.", "OK");
            return;
        }

        var session = await _databaseService.GetUserSessionAsync();
        session.CurrentUserId = savedUser.Id;
        session.LastAccessDate = DateTime.Today;
        await _databaseService.SaveUserSessionAsync(session);

        await _soundService.PlaySuccessAsync();
        Application.Current.Windows[0].Page = new AppShell();
    }
}
