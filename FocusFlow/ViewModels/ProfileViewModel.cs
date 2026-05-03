using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusFlow.Models;
using FocusFlow.Services;
using CommunityToolkit.Mvvm.Input;
// 👇 IMPORTANTE: Añadimos esto para poder hablar con el centro de notificaciones
using Plugin.LocalNotification;

namespace FocusFlow.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly SoundService _soundService;

    [ObservableProperty]
    private UserProfile currentUserProfile;

    [ObservableProperty]
    private ObservableCollection<AchievementItem> achievements = new();

    [ObservableProperty]
    private int maxCustomRewards;

    [ObservableProperty]
    private int currentCustomRewards;

    public ProfileViewModel(DatabaseService databaseService, SoundService soundService)
    {
        _databaseService = databaseService;
        _soundService = soundService;
        currentUserProfile = new UserProfile();
    }

    public async Task LoadProfileDataAsync()
    {
        var session = await _databaseService.GetUserSessionAsync();
        CurrentUserProfile = await _databaseService.GetUserProfileAsync(session.CurrentUserId);

        // 🏆 PEDIMOS LOS LOGROS SOLO DEL USUARIO ACTUAL
        var achievementList = await _databaseService.GetAchievementsAsync(session.CurrentUserId);
        Achievements = new ObservableCollection<AchievementItem>(achievementList);

        var rewards = await _databaseService.GetRewardsAsync(session.CurrentUserId);
        CurrentCustomRewards = rewards.Count(r => !r.IsSystemReward);
        MaxCustomRewards = 15 + (CurrentUserProfile.Level * 5);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var session = await _databaseService.GetUserSessionAsync();
        session.CurrentUserId = 0;
        session.LastAccessDate = DateTime.Today;
        await _databaseService.SaveUserSessionAsync(session);

        await _soundService.PlayLogoutAsync();

        // 🧹 ¡MAGIA LIMPIADORA! Cancelamos y borramos TODAS las notificaciones de Android
        LocalNotificationCenter.Current.CancelAll();
        LocalNotificationCenter.Current.ClearAll();

        Application.Current.Windows[0].Page = new LoginPage(_databaseService, _soundService);
    }
}