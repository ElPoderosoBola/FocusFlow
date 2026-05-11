using FocusFlow.Services;

namespace FocusFlow;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    // Se dispara cuando cambias de page
    protected override async void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        try
        {
            // Reproducimos el click recogiendo el SoundService
            var soundService = Application.Current?.Handler?.MauiContext?.Services.GetService<SoundService>();
            if (soundService != null)
            {
                await soundService.PlayClickAsync();
            }
        }
        catch
        {
            // Porsiacaso ha habido un error
        }
    }
}