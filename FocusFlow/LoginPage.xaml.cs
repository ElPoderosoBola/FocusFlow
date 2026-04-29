using FocusFlow.Services;
using FocusFlow.Models;

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

    private async void OnLoginClicked(object sender, EventArgs e)
    {

        string username = UsernameEntry.Text?.Trim();
        string password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Aviso", "¡Necesitas tu nombre y contraseña para entrar!", "OK");
            return;
        }

        var user = await _databaseService.LoginUserAsync(username, password);

        if (user != null)
        {
            var session = await _databaseService.GetUserSessionAsync();
            session.CurrentUserId = user.Id;
            await _databaseService.SaveUserSessionAsync(session);

            await _soundService.PlayLoginAsync(); // <-- ¡Suena Chimes.mp3 al logearse!

            Application.Current.Windows[0].Page = new AppShell();
        }
        else
        {
            await _soundService.PlayFailAsync();
            await DisplayAlert("Error", "El nombre o la contraseña no coinciden.", "Vaya...");
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {

        string username = UsernameEntry.Text?.Trim();
        string password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Aviso", "Escribe un nombre y contraseña para registrarte.", "OK");
            return;
        }

        var newUser = new User { Username = username, Password = password };
        var success = await _databaseService.RegisterUserAsync(newUser);

        if (success)
        {
            await _soundService.PlayCreatedAsync(); // <-- Suena SonidoNormal.mp3
            await DisplayAlert("¡Bienvenido!", "Tu cuenta ha sido creada. Ahora dale a ENTRAR.", "¡Genial!");
        }
        else
        {
            await _soundService.PlayFailAsync();
            await DisplayAlert("Error", "Ese nombre de héroe ya está pillado. Elige otro.", "OK");
        }
    }
}