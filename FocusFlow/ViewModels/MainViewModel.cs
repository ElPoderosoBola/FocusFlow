using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusFlow.Models;
using FocusFlow.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.AndroidOption;

namespace FocusFlow.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private bool _isInitialized;
    private IDispatcherTimer _timer;
    private int _currentUserId = 1;
    private string? _pendingImagePath;

    // Lista observable para que la UI se actualice cuando cambien las tareas.
    [ObservableProperty]
    private ObservableCollection<TaskItem> tasks = new();

    // Lista observable de hábitos.
    [ObservableProperty]
    private ObservableCollection<HabitItem> habits = new();

    // Lista observable de dailies.
    [ObservableProperty]
    private ObservableCollection<DailyItem> dailies = new();

    // Lista observable de recompensas.
    [ObservableProperty]
    private ObservableCollection<RewardItem> rewards = new();

    // Perfil actual del usuario (nivel y experiencia persistidos).
    [ObservableProperty]
    private UserProfile currentUserProfile = new() { Id = 1, Level = 1, CurrentXP = 0, Coins = 0, Health = 50, MaxHealth = 50 };

    // Indica si el temporizador está en marcha.
    [ObservableProperty]
    private bool isTimerRunning = false;

    // Texto del temporizador en formato MM:ss.
    [ObservableProperty]
    private string timerDisplay = "25:00";

    // Segundos restantes del temporizador.
    [ObservableProperty]
    private int timeRemainingSeconds = 1500;

    public MainViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;

        // Inicializa el temporizador de concentración.
        _timer = Application.Current.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
    }

    private async void Timer_Tick(object sender, EventArgs e)
    {
        TimeRemainingSeconds--;

        var minutes = TimeRemainingSeconds / 60;
        var seconds = TimeRemainingSeconds % 60;
        TimerDisplay = $"{minutes:00}:{seconds:00}";

        if (TimeRemainingSeconds <= 0)
        {
            _timer.Stop();
            IsTimerRunning = false;
            TimeRemainingSeconds = 0;
            TimerDisplay = "00:00";

            await Application.Current.MainPage.DisplayAlert(
                "Temporizador",
                "¡Misión Completada! Has ganado 50 XP",
                "OK");

            await AddExperienceAsync(50, 10);
            await CheckAchievementsAsync();
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        // Carga inicial de datos cuando el ViewModel está listo para usarse.
        CurrentUserProfile = await _databaseService.GetUserProfileAsync();
        await LoadTasksAsync();
        await LoadHabitsAsync();
        await LoadDailiesAsync();

        // Aplica penalización diaria si pasó al menos un día desde el último acceso.
        if (CurrentUserProfile.LastLoginDate.Date < DateTime.Today)
        {
            var misionesFalladas = Dailies.Count(d => !d.IsCompletedToday);
            int damage = misionesFalladas * 10;

            if (damage > 0)
            {
                CurrentUserProfile.Health -= damage;

                // Si no queda salud, aplica la lógica de Game Over.
                if (CurrentUserProfile.Health <= 0)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "¡Caíste en batalla!",
                        "Has perdido toda tu salud. Pierdes 1 Nivel y 10 monedas.",
                        "Resucitar");

                    CurrentUserProfile.Level = Math.Max(1, CurrentUserProfile.Level - 1);
                    CurrentUserProfile.Coins = Math.Max(0, CurrentUserProfile.Coins - 10);
                    CurrentUserProfile.CurrentXP = 0;
                    CurrentUserProfile.Health = CurrentUserProfile.MaxHealth;
                }
            }

            // Reinicia todos los dailies para el nuevo día y los guarda.
            foreach (var daily in Dailies)
            {
                daily.IsCompletedToday = false;
                await _databaseService.SaveDailyAsync(daily);
            }

            // Guarda la nueva fecha de acceso y el perfil actualizado.
            CurrentUserProfile.LastLoginDate = DateTime.Today;
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));
            await LoadDailiesAsync();
        }

        await LoadRewardsAsync();
        await ScheduleDailyReminderAsync();
        _isInitialized = true;
    }

    private async Task ScheduleDailyReminderAsync()
    {
        // Pide permisos de notificación si todavía no están aceptados.
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
        {
            await LocalNotificationCenter.Current.RequestNotificationPermission();
        }

        // Programa recordatorio diario para que no se pierda la racha.
        var request = new NotificationRequest
        {
            NotificationId = 100,
            Title = "¡Despierta, Héroe!",
            Description = "Tus misiones diarias te esperan. ¡No pierdas tu racha y evita recibir daño!",
            Schedule = new NotificationRequestSchedule { NotifyTime = DateTime.Now.AddSeconds(10) }
        };

        await LocalNotificationCenter.Current.Show(request);
    }

    public async Task LoadTasksAsync()
    {
        var taskList = await _databaseService.GetTasksAsync(_currentUserId);
        Tasks = new ObservableCollection<TaskItem>(taskList);
    }

    public async Task LoadHabitsAsync()
    {
        var habitList = await _databaseService.GetHabitsAsync(_currentUserId);
        Habits = new ObservableCollection<HabitItem>(habitList);
    }

    public async Task LoadDailiesAsync()
    {
        var dailyList = await _databaseService.GetDailiesAsync(_currentUserId);
        Dailies = new ObservableCollection<DailyItem>(dailyList);
    }

    public async Task LoadRewardsAsync()
    {
        var rewardList = await _databaseService.GetRewardsAsync(_currentUserId);
        Rewards = new ObservableCollection<RewardItem>(rewardList);
    }

    private async Task AddExperienceAsync(int amount, int coinsToAdd = 0)
    {
        CurrentUserProfile.CurrentXP += amount;
        CurrentUserProfile.Coins += coinsToAdd;

        while (CurrentUserProfile.CurrentXP >= 100)
        {
            CurrentUserProfile.Level++;
            CurrentUserProfile.CurrentXP -= 100;
        }

        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));
    }

    private async Task CheckAchievementsAsync()
    {
        var achievements = await _databaseService.GetAchievementsAsync();
        var unlockedAny = false;

        var apprentice = achievements.FirstOrDefault(a => a.Title == "Aprendiz");
        if (apprentice is not null && !apprentice.IsUnlocked && CurrentUserProfile.Level >= 2)
        {
            apprentice.IsUnlocked = true;
            CurrentUserProfile.Coins += 20;
            await _databaseService.UpdateAchievementAsync(apprentice);
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);

            await Application.Current.MainPage.DisplayAlert("🏆 ¡Logro Desbloqueado!", "Aprendiz: Alcanza el Nivel 2.\n¡Ganas 20 monedas!", "¡Genial!");
            unlockedAny = true;
        }

        var hoarder = achievements.FirstOrDefault(a => a.Title == "Acaparador");
        if (hoarder is not null && !hoarder.IsUnlocked && CurrentUserProfile.Coins >= 50)
        {
            hoarder.IsUnlocked = true;
            CurrentUserProfile.Coins += 20;
            await _databaseService.UpdateAchievementAsync(hoarder);
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);

            await Application.Current.MainPage.DisplayAlert("🏆 ¡Logro Desbloqueado!", "Acaparador: Consigue 50 monedas.\n¡Ganas 20 monedas!", "¡Genial!");
            unlockedAny = true;
        }

        var firstSteps = achievements.FirstOrDefault(a => a.Title == "Primeros Pasos");
        var hasProgress = CurrentUserProfile.CurrentXP > 0 || Tasks.Any(t => t.IsCompleted) || Dailies.Any(d => d.IsCompletedToday);
        if (firstSteps is not null && !firstSteps.IsUnlocked && hasProgress)
        {
            firstSteps.IsUnlocked = true;
            CurrentUserProfile.Coins += 20;
            await _databaseService.UpdateAchievementAsync(firstSteps);
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);

            await Application.Current.MainPage.DisplayAlert("🏆 ¡Logro Desbloqueado!", "Primeros Pasos: Completa tu primera misión diaria.\n¡Ganas 20 monedas!", "¡Genial!");
            unlockedAny = true;
        }

        if (unlockedAny)
        {
            OnPropertyChanged(nameof(CurrentUserProfile));
        }
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        var photo = await MediaPicker.Default.PickPhotoAsync();
        _pendingImagePath = photo?.FullPath;
    }

    [RelayCommand]
    private async Task AddNewHabitAsync()
    {
        var title = await Application.Current.MainPage.DisplayPromptAsync("Nuevo Hábito", "¿Qué hábito quieres registrar?");
        if (string.IsNullOrWhiteSpace(title)) return;

        var newHabit = new HabitItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            IsPositive = true,
            IsNegative = false,
            ImagePath = _pendingImagePath
        };

        await _databaseService.SaveHabitAsync(newHabit);
        Habits.Add(newHabit);
        _pendingImagePath = null;
    }

    [RelayCommand]
    private async Task AddNewDailyAsync()
    {
        var title = await Application.Current.MainPage.DisplayPromptAsync("Nuevo Daily", "¿Qué daily quieres registrar?");
        if (string.IsNullOrWhiteSpace(title)) return;

        var newDaily = new DailyItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            IsCompletedToday = false,
            LastCompletedDate = DateTime.MinValue,
            Streak = 0,
            ImagePath = _pendingImagePath
        };

        await _databaseService.SaveDailyAsync(newDaily);
        Dailies.Add(newDaily);
        _pendingImagePath = null;
    }

    [RelayCommand]
    private async Task AddNewTaskAsync()
    {
        var title = await Application.Current.MainPage.DisplayPromptAsync("Nueva Tarea", "¿Qué tarea quieres registrar?");
        if (string.IsNullOrWhiteSpace(title)) return;

        var newTask = new TaskItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            EstimatedMinutes = 25,
            IsCompleted = false,
            ImagePath = _pendingImagePath
        };

        await _databaseService.SaveTaskAsync(newTask);
        Tasks.Add(newTask);
        _pendingImagePath = null;
    }

    [RelayCommand]
    private async Task AddNewRewardAsync()
    {
        var title = await Application.Current.MainPage.DisplayPromptAsync("Nueva Recompensa", "¿Qué recompensa quieres añadir?");
        if (string.IsNullOrWhiteSpace(title)) return;

        var costText = await Application.Current.MainPage.DisplayPromptAsync("Coste de Recompensa", "¿Cuántas monedas cuesta?");
        if (string.IsNullOrWhiteSpace(costText) || !int.TryParse(costText, out var cost) || cost < 0) return;

        var newReward = new RewardItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            Cost = cost,
            ImagePath = _pendingImagePath
        };

        await _databaseService.SaveRewardAsync(newReward);
        Rewards.Add(newReward);
        _pendingImagePath = null;
    }

    // ========================================================
    // AQUI ESTÁN LOS 3 COMANDOS DE BORRADO ACTUALIZADOS (CON CARTEL DE CONFIRMACIÓN)
    // ========================================================

    [RelayCommand]
    private async Task DeleteTaskAsync(TaskItem task)
    {
        if (task is null) return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Eliminar Tarea",
            "¿Estás seguro de que quieres borrar esta tarea?",
            "Sí",
            "No");

        if (confirm)
        {
            await _databaseService.DeleteTaskAsync(task);
            Tasks.Remove(task);
        }
    }

    [RelayCommand]
    private async Task DeleteHabitAsync(HabitItem habit)
    {
        if (habit is null) return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Eliminar Hábito",
            "¿Estás seguro de que quieres borrar este hábito?",
            "Sí",
            "No");

        if (confirm)
        {
            await _databaseService.DeleteHabitAsync(habit);
            Habits.Remove(habit);
        }
    }

    [RelayCommand]
    private async Task DeleteDailyAsync(DailyItem daily)
    {
        if (daily is null) return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Eliminar Diaria",
            "¿Estás seguro de que quieres borrar esta tarea diaria?",
            "Sí",
            "No");

        if (confirm)
        {
            await _databaseService.DeleteDailyAsync(daily);
            Dailies.Remove(daily);
        }
    }

    // ========================================================

    [RelayCommand]
    private async Task CompleteTaskAsync(TaskItem task)
    {
        if (task is null || task.IsCompleted) return;

        task.IsCompleted = true;
        await _databaseService.SaveTaskAsync(task);

        await AddExperienceAsync(50, 10);
        await LoadTasksAsync();
        await CheckAchievementsAsync();
    }

    [RelayCommand]
    private Task StartTimerAsync()
    {
        if (IsTimerRunning) return Task.CompletedTask;

        IsTimerRunning = true;
        _timer.Start();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task StopTimerAsync()
    {
        _timer.Stop();

        var surrender = await Application.Current.MainPage.DisplayAlert("Detener temporizador", "¿Te rindes en esta misión?", "Sí", "No");

        if (surrender)
        {
            TimeRemainingSeconds = 1500;
            TimerDisplay = "25:00";
            IsTimerRunning = false;
            return;
        }

        IsTimerRunning = true;
        _timer.Start();
    }

    [RelayCommand]
    private async Task ExecuteHabitAsync(HabitItem habit)
    {
        if (habit is null) return;

        if (habit.IsPositive)
        {
            await AddExperienceAsync(10, 10);
        }

        if (habit.IsNegative)
        {
            CurrentUserProfile.Health -= 10;

            if (CurrentUserProfile.Health <= 0)
            {
                await Application.Current.MainPage.DisplayAlert("¡Caíste en batalla!", "Has perdido toda tu salud. Pierdes 1 Nivel y 10 monedas.", "Resucitar");
                CurrentUserProfile.Level = Math.Max(1, CurrentUserProfile.Level - 1);
                CurrentUserProfile.Coins = Math.Max(0, CurrentUserProfile.Coins - 10);
                CurrentUserProfile.CurrentXP = 0;
                CurrentUserProfile.Health = CurrentUserProfile.MaxHealth;
            }
        }

        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));

        await _databaseService.SaveHabitAsync(habit);
        await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task CompleteDailyAsync(DailyItem daily)
    {
        if (daily is null) return;

        daily.IsCompletedToday = true;
        daily.LastCompletedDate = DateTime.Today;

        await AddExperienceAsync(20, 10);
        await _databaseService.SaveDailyAsync(daily);

        await LoadDailiesAsync();
        await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task BuyRewardAsync(RewardItem reward)
    {
        if (reward is null) return;

        if (CurrentUserProfile.Coins >= reward.Cost)
        {
            CurrentUserProfile.Coins -= reward.Cost;
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));

            await Application.Current.MainPage.DisplayAlert("Recompensas", "¡Premio comprado! Disfruta de tu recompensa", "OK");
            return;
        }

        await Application.Current.MainPage.DisplayAlert("Recompensas", "No tienes suficientes monedas", "OK");
    }
}