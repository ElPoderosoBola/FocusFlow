using Microsoft.Extensions.DependencyInjection;
using FocusFlow.Services;

namespace FocusFlow
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var databaseService = IPlatformApplication.Current.Services.GetService<DatabaseService>();
            var soundService = IPlatformApplication.Current.Services.GetService<SoundService>();

            if (databaseService is null || soundService is null)
            {
                return new Window(new AppShell());
            }

            var session = Task.Run(() => databaseService.GetUserSessionAsync()).GetAwaiter().GetResult();
            var startPage = session.CurrentUserId > 0 ? (Page)new AppShell() : new LoginPage(databaseService, soundService);
            return new Window(startPage);
        }
    }
}