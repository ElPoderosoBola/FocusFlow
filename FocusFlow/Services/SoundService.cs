using Plugin.Maui.Audio;

namespace FocusFlow.Services;

public class SoundService
{
    private readonly IAudioManager _audioManager;

    public SoundService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public async Task PlayClickAsync()
    {
        await PlaySoundAsync("click.mp3");
    }

    public async Task PlaySuccessAsync()
    {
        await PlaySoundAsync("success.mp3");
    }

    public async Task PlayFailAsync()
    {
        await PlaySoundAsync("fail.mp3");
    }

    private async Task PlaySoundAsync(string fileName)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            var player = _audioManager.CreatePlayer(stream);
            player.Play();
        }
        catch
        {
            // Si el archivo no existe aún, no bloqueamos la app.
        }
    }
}
