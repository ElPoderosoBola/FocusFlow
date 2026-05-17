using Plugin.Maui.Audio;

namespace FocusFlow.Services;

public class SoundService
{
    private async Task PlaySoundAsync(string filename, double volume = 1.0)
    {
        try
        {
            var player = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync(filename));


            player.Volume = volume;

            player.Play();
        }
        catch
        {

        }
    }

    public Task PlayFailAsync() => PlaySoundAsync("MisionFallida.mp3");
    public Task PlayTaDaAsync() => PlaySoundAsync("TaDa.mp3");
    public Task PlayLoginAsync() => PlaySoundAsync("Chimes.mp3");
    public Task PlayLogoutAsync() => PlaySoundAsync("Shutdown.mp3");
    public Task PlayRewardBoughtAsync() => PlaySoundAsync("SpeechOn.mp3");
    public Task PlayCreatedAsync() => PlaySoundAsync("SonidoNormal.mp3");


    public Task PlayDeletedAsync() => PlaySoundAsync("Trash.mp3", 0.3);


    public Task PlayClickAsync() => PlaySoundAsync("PopUp.mp3");
}