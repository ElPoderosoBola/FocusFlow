using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusFlow.Models;
using FocusFlow.Services;

namespace FocusFlow.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private UserProfile currentUserProfile;

    [ObservableProperty]
    private ObservableCollection<AchievementItem> achievements = new();

    public ProfileViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        currentUserProfile = new UserProfile();
    }

    public async Task LoadProfileDataAsync()
    {
        // Carga perfil y logros desde SQLite para mostrarlos en pantalla.
        CurrentUserProfile = await _databaseService.GetUserProfileAsync();
        var achievementList = await _databaseService.GetAchievementsAsync();
        Achievements = new ObservableCollection<AchievementItem>(achievementList);
    }
}
