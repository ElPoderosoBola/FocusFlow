using FocusFlow.Services;

namespace FocusFlow;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    // 🪄 Este evento se dispara cada vez que cambias de pestaña
    protected override async void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        try
        {
            // Buscamos tu servicio de sonido en las tripas de la app y le damos al Play
            var soundService = Application.Current?.Handler?.MauiContext?.Services.GetService<SoundService>();
            if (soundService != null)
            {
                await soundService.PlayClickAsync();
            }
        }
        catch
        {
            // Si hay algún micro-corte al cargar, nos quedamos callados para no molestar
        }
    }
}