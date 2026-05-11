using FocusFlow.Services;
using FocusFlow.Models;

namespace FocusFlow;

public partial class LoginPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly SoundService _soundService;

    // Task para esperar a que pulses "ENTENDIDO"
    private TaskCompletionSource<bool> _alertTcs;

    public LoginPage(DatabaseService databaseService, SoundService soundService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _soundService = soundService;
    }

    // Alert custom que he fabricado
    private async Task ShowAeroAlert(string title, string message)
    {
        AlertTitleLabel.Text = title;
        AlertMessageLabel.Text = message;
        AlertOverlay.IsVisible = true;

        _alertTcs = new TaskCompletionSource<bool>();
        await _alertTcs.Task; // El código se pausa aquí hasta que se pulse el botón
    }

    private async void OnCloseAlertClicked(object sender, EventArgs e)
    {
        await _soundService.PlayClickAsync();
        AlertOverlay.IsVisible = false; // Escondemos el alert
        _alertTcs?.TrySetResult(true);  // Continúa la ejecución
    }
    // ------------------------------------

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string username = UsernameEntry.Text?.Trim();
        string password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Aviso", "¡Necesitas tu nombre y contraseña para entrar!");
            return;
        }

        var user = await _databaseService.LoginUserAsync(username, password);

        if (user != null)
        {
            var session = await _databaseService.GetUserSessionAsync();
            session.CurrentUserId = user.Id;
            await _databaseService.SaveUserSessionAsync(session);

            await _soundService.PlayLoginAsync();
            Application.Current.Windows[0].Page = new AppShell();
        }
        else
        {
            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Error", "El nombre o la contraseña no coinciden.");
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        string username = UsernameEntry.Text?.Trim();
        string password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Aviso", "Escribe un nombre y contraseña para registrarte.");
            return;
        }

        var newUser = new User { Username = username, Password = password };
        var success = await _databaseService.RegisterUserAsync(newUser);

        if (success)
        {
            await _soundService.PlayCreatedAsync();
            await ShowAeroAlert("¡Bienvenido!", "Tu cuenta ha sido creada.\nAhora dale a ENTRAR AL JUEGO.");
        }
        else
        {
            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Error", "Ese nombre  ya está pillado. Elige otro.");
        }
    }
}