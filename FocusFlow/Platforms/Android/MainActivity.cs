using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace FocusFlow;

// Fíjate que aquí ya pone "@style/Maui.SplashTheme" limpio, sin cosas raras
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // 🪄 ¡MAGIA PARA LAS BARRAS!
        // Pintamos el techo (Status Bar) y el suelo (Navigation Bar) de azul oscuro
        var azulOscuro = Android.Graphics.Color.ParseColor("#162533");
        Window?.SetStatusBarColor(azulOscuro);
        Window?.SetNavigationBarColor(azulOscuro);
    }
}