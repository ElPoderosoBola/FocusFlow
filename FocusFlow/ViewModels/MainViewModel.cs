using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusFlow.Models;
using FocusFlow.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.AndroidOption;
using System.Globalization;

namespace FocusFlow.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly SoundService _soundService;
    private bool _isInitialized;
    private IDispatcherTimer _timer;
    private int _currentUserId = 1;
    private string? _pendingImagePath;

    [ObservableProperty]
    private ObservableCollection<TaskItem> tasks = new();

    [ObservableProperty]
    private ObservableCollection<HabitItem> habits = new();

    [ObservableProperty]
    private ObservableCollection<RewardItem> rewards = new();

    [ObservableProperty]
    private UserProfile currentUserProfile = new() { Id = 1, Level = 1, CurrentXP = 0, Coins = 0, Health = 50, MaxHealth = 50 };

    public MainViewModel(DatabaseService databaseService, SoundService soundService)
    {
        _databaseService = databaseService;
        _soundService = soundService;

        _timer = Application.Current.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(1);
        _timer.Tick += async (_, _) => await CheckMissionDeadlinesAsync();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        var session = await _databaseService.GetUserSessionAsync();
        _currentUserId = session.CurrentUserId;

        if (_currentUserId == 0) return;

        CurrentUserProfile = await _databaseService.GetUserProfileAsync(_currentUserId);
        await LoadTasksAsync();
        await LoadHabitsAsync();

        CurrentUserProfile.LastLoginDate = DateTime.Today;
        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));

        await LoadRewardsAsync();
        await ScheduleDailyReminderAsync();
        _timer.Start();
        _isInitialized = true;
    }

    private async Task ApplyGameOverAsync()
    {
        await Application.Current.MainPage.DisplayAlert("¡Caíste en batalla!", "Has perdido toda tu salud. Pierdes 1 Nivel y 50 monedas.", "Resucitar");

        CurrentUserProfile.Level = Math.Max(0, CurrentUserProfile.Level - 1);
        CurrentUserProfile.Coins = Math.Max(0, CurrentUserProfile.Coins - 50);
        CurrentUserProfile.CurrentXP = 0;
        CurrentUserProfile.Health = CurrentUserProfile.MaxHealth;

        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));
    }

    private async Task NotifyMissionFailedAsync(string missionTitle)
    {
        var request = new NotificationRequest
        {
            NotificationId = 3000 + Random.Shared.Next(1, 999),
            Title = "Misión fallida",
            Description = $"Misión fallida. No has completado {missionTitle}",
            Schedule = new NotificationRequestSchedule { NotifyTime = DateTime.Now.AddSeconds(1) }
        };
        await LocalNotificationCenter.Current.Show(request);
    }

    private async Task ScheduleDailyReminderAsync()
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
        {
            await LocalNotificationCenter.Current.RequestNotificationPermission();
        }

        var request = new NotificationRequest
        {
            NotificationId = 100,
            Title = "¡Despierta, Héroe!",
            Description = "Tus misiones te esperan. ¡No pierdas tu racha y evita recibir daño!",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = DateTime.Today.AddDays(1).AddHours(9),
                RepeatType = NotificationRepeat.Daily
            }
        };
        await LocalNotificationCenter.Current.Show(request);
        await ScheduleUpcomingMissionNotificationsAsync();
    }

    private async Task ScheduleUpcomingMissionNotificationsAsync()
    {
        var now = DateTime.Now;

        foreach (var task in Tasks.Where(t => !t.IsCompleted && !t.IsFailed))
        {
            var notifyTime = task.DueDateTime.AddMinutes(-30);
            if (notifyTime > now)
            {
                var taskRequest = new NotificationRequest
                {
                    NotificationId = 1000 + task.Id,
                    Title = "Aviso de misión",
                    Description = $"Solo queda media hora para {task.Title} ¿Lo acabaste?",
                    Schedule = new NotificationRequestSchedule { NotifyTime = notifyTime }
                };
                await LocalNotificationCenter.Current.Show(taskRequest);
            }
        }

        foreach (var habit in Habits)
        {
            if (string.IsNullOrWhiteSpace(habit.ActiveDaysCsv)) continue;

            var today = DateTime.Today;
            var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
            var activeDays = habit.ActiveDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (activeDays.Contains(todayCode))
            {
                var habitDateTime = today.Add(habit.ScheduledTime);
                var notifyTime = habitDateTime.AddMinutes(-30);
                if (notifyTime > now)
                {
                    var habitRequest = new NotificationRequest
                    {
                        NotificationId = 2000 + habit.Id,
                        Title = "Aviso de misión",
                        Description = $"Solo queda media hora para {habit.Title} ¿Lo acabaste?",
                        Schedule = new NotificationRequestSchedule { NotifyTime = notifyTime }
                    };
                    await LocalNotificationCenter.Current.Show(habitRequest);
                }
            }
        }
    }

    private async Task CheckMissionDeadlinesAsync()
    {
        var now = DateTime.Now;

        foreach (var task in Tasks.Where(t => !t.IsCompleted && !t.IsFailed && t.DueDateTime <= now).ToList())
        {
            task.IsFailed = true;
            await _databaseService.SaveTaskAsync(task);

            CurrentUserProfile.Health -= 10;
            await _soundService.PlayFailAsync();
            await NotifyMissionFailedAsync(task.Title);
            await Application.Current.MainPage.DisplayAlert("Fallida", $"No has completado {task.Title}", "OK");
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        }

        foreach (var habit in Habits)
        {
            var today = DateTime.Today;
            var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
            var activeDays = (habit.ActiveDaysCsv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!activeDays.Contains(todayCode)) continue;

            var habitDeadline = today.Add(habit.ScheduledTime);
            var alreadyCompletedToday = habit.LastCompletedDate.Date == today;
            var alreadyPenalizedToday = habit.LastPenaltyDate.Date == today;

            if (!alreadyCompletedToday && !alreadyPenalizedToday && habitDeadline <= now)
            {
                CurrentUserProfile.Health -= 5;
                habit.LastPenaltyDate = today;
                await _databaseService.SaveHabitAsync(habit);

                await _soundService.PlayFailAsync();
                await NotifyMissionFailedAsync(habit.Title);
                await Application.Current.MainPage.DisplayAlert("Fallida", $"No has completado {habit.Title}", "OK");
                await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            }
        }

        if (CurrentUserProfile.Health <= 0) await ApplyGameOverAsync();
    }

    // 💡 ¡EL FILTRO MÁGICO! Ahora las tareas completadas se ocultan visualmente
    public async Task LoadTasksAsync()
    {
        var allTasks = await _databaseService.GetTasksAsync(_currentUserId);
        Tasks = new ObservableCollection<TaskItem>(allTasks.Where(t => !t.IsCompleted && !t.IsFailed));
    }

    public async Task LoadHabitsAsync() { Habits = new ObservableCollection<HabitItem>(await _databaseService.GetHabitsAsync(_currentUserId)); }
    public async Task LoadRewardsAsync() { Rewards = new ObservableCollection<RewardItem>(await _databaseService.GetRewardsAsync(_currentUserId)); }

    private async Task AddExperienceAsync(int amount, int coinsToAdd = 0)
    {
        CurrentUserProfile.CurrentXP += amount;
        CurrentUserProfile.Coins += coinsToAdd;

        while (CurrentUserProfile.CurrentXP >= GetXpNeededForLevel(CurrentUserProfile.Level))
        {
            CurrentUserProfile.CurrentXP -= GetXpNeededForLevel(CurrentUserProfile.Level);
            CurrentUserProfile.Level++;
        }

        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));
    }

    private static int GetXpNeededForLevel(int level) => 50 + (level * 5);

    private async Task CheckAchievementsAsync()
    {
        var achievements = await _databaseService.GetAchievementsAsync();
        var unlockedAny = false;
        string unlockedTitles = "";

        var apprentice = achievements.FirstOrDefault(a => a.Title == "Aprendiz");
        if (apprentice is not null && !apprentice.IsUnlocked && CurrentUserProfile.Level >= 2)
        {
            apprentice.IsUnlocked = true;
            CurrentUserProfile.Coins += 20;
            await _databaseService.UpdateAchievementAsync(apprentice);
            unlockedAny = true;
            unlockedTitles += "🏅 Aprendiz (¡Por subir a Nivel 2!)\n";
        }

        var hoarder = achievements.FirstOrDefault(a => a.Title == "Acaparador");
        if (hoarder is not null && !hoarder.IsUnlocked && CurrentUserProfile.Coins >= 50)
        {
            hoarder.IsUnlocked = true;
            CurrentUserProfile.Coins += 20;
            await _databaseService.UpdateAchievementAsync(hoarder);
            unlockedAny = true;
            unlockedTitles += "🏅 Acaparador (¡Por ahorrar 50 monedas!)\n";
        }

        var firstSteps = achievements.FirstOrDefault(a => a.Title == "Primeros Pasos");
        var hasProgress = CurrentUserProfile.CurrentXP > 0 || Tasks.Any(t => t.IsCompleted) || Habits.Any(h => h.LastCompletedDate.Date == DateTime.Today);
        if (firstSteps is not null && !firstSteps.IsUnlocked && hasProgress)
        {
            firstSteps.IsUnlocked = true;
            CurrentUserProfile.Coins += 20;
            await _databaseService.UpdateAchievementAsync(firstSteps);
            unlockedAny = true;
            unlockedTitles += "🏅 Primeros Pasos (¡Por arrancar a moverte!)\n";
        }

        if (unlockedAny)
        {
            await _soundService.PlayTaDaAsync();

            await Application.Current.MainPage.DisplayAlert(
                "¡NUEVO LOGRO DESBLOQUEADO!",
                $"Has conseguido:\n\n{unlockedTitles}\n¡Y te llevas monedas extra de premio!",
                "¡SOY GENIAL!");

            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
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
        await _soundService.PlayClickAsync();

        bool attachImg = await Application.Current.MainPage.DisplayAlert("Imagen", "¿Quieres añadir una foto de tu galería?", "Sí", "No");
        if (attachImg) await PickImageAsync();
        else _pendingImagePath = null;

        var title = await Application.Current.MainPage.DisplayPromptAsync("Nuevo Hábito", "Nombre:");
        if (string.IsNullOrWhiteSpace(title)) return;

        var rewardText = await Application.Current.MainPage.DisplayPromptAsync("Recompensa", "¿Cuántas monedas da?");
        if (string.IsNullOrWhiteSpace(rewardText) || !int.TryParse(rewardText, out var rewardCoins) || rewardCoins < 0) return;

        var timeText = await Application.Current.MainPage.DisplayPromptAsync("Hora", "Hora límite (HH:mm):");
        if (string.IsNullOrWhiteSpace(timeText) || !TimeSpan.TryParseExact(timeText, "hh\\:mm", CultureInfo.InvariantCulture, out var scheduledTime)) return;

        var daysText = await Application.Current.MainPage.DisplayPromptAsync(
            "Días activos",
            "Días (0=Dom..6=Sáb) por coma. Ej: 1,3,5");

        if (string.IsNullOrWhiteSpace(daysText)) return;

        var dayTokens = daysText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => int.TryParse(x, out var day) && day >= 0 && day <= 6).Distinct().ToArray();

        var newHabit = new HabitItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            ScheduledTime = scheduledTime,
            ActiveDaysCsv = string.Join(',', dayTokens),
            RewardCoins = rewardCoins,
            IsPositive = true,
            ImagePath = _pendingImagePath
        };

        await _databaseService.SaveHabitAsync(newHabit);
        Habits.Add(newHabit);
        await _soundService.PlayCreatedAsync();
        _pendingImagePath = null;
    }

    [RelayCommand]
    private async Task AddNewTaskAsync()
    {
        await _soundService.PlayClickAsync();

        bool attachImg = await Application.Current.MainPage.DisplayAlert("Imagen", "¿Quieres añadir una foto de tu galería?", "Sí", "No");
        if (attachImg) await PickImageAsync();
        else _pendingImagePath = null;

        var title = await Application.Current.MainPage.DisplayPromptAsync("Nueva Tarea", "Nombre:");
        if (string.IsNullOrWhiteSpace(title)) return;

        var rewardText = await Application.Current.MainPage.DisplayPromptAsync("Recompensa", "¿Cuántas monedas da?", initialValue: "10");
        if (string.IsNullOrWhiteSpace(rewardText) || !int.TryParse(rewardText, out var rewardCoins) || rewardCoins < 0) return;

        var dateText = await Application.Current.MainPage.DisplayPromptAsync("Fecha límite", "Vencimiento (yyyy-MM-dd):", initialValue: DateTime.Today.ToString("yyyy-MM-dd"));
        var timeText = await Application.Current.MainPage.DisplayPromptAsync("Hora límite", "Hora (HH:mm):", initialValue: DateTime.Now.AddHours(1).ToString("HH:mm"));

        if (string.IsNullOrWhiteSpace(dateText) || string.IsNullOrWhiteSpace(timeText) ||
            !DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueDate) ||
            !TimeSpan.TryParseExact(timeText, "hh\\:mm", CultureInfo.InvariantCulture, out var dueTime)) return;

        var newTask = new TaskItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            DueDateTime = dueDate.Date.Add(dueTime),
            RewardCoins = rewardCoins,
            IsCompleted = false,
            IsFailed = false,
            ImagePath = _pendingImagePath
        };

        await _databaseService.SaveTaskAsync(newTask);
        Tasks.Add(newTask);
        await _soundService.PlayCreatedAsync();
        _pendingImagePath = null;
    }

    [RelayCommand]
    private async Task AddNewRewardAsync()
    {
        await _soundService.PlayClickAsync();

        var maxCustomRewards = 15 + (CurrentUserProfile.Level * 5);
        if (Rewards.Count(r => !r.IsSystemReward) >= maxCustomRewards)
        {
            await Application.Current.MainPage.DisplayAlert("Límite", $"Alcanzaste el tope ({maxCustomRewards}). ¡Sube de nivel!", "OK");
            return;
        }

        bool attachImg = await Application.Current.MainPage.DisplayAlert("Imagen", "¿Quieres añadir una foto de tu galería?", "Sí", "No");
        if (attachImg) await PickImageAsync();
        else _pendingImagePath = null;

        var title = await Application.Current.MainPage.DisplayPromptAsync("Nueva Recompensa", "¿Qué capricho?");
        if (string.IsNullOrWhiteSpace(title)) return;

        var costText = await Application.Current.MainPage.DisplayPromptAsync("Coste", "¿Cuántas monedas?");
        if (string.IsNullOrWhiteSpace(costText) || !int.TryParse(costText, out var cost) || cost < 0) return;

        var newReward = new RewardItem { UserId = _currentUserId, Title = title.Trim(), Cost = cost, ImagePath = _pendingImagePath };
        await _databaseService.SaveRewardAsync(newReward);
        Rewards.Add(newReward);
        await _soundService.PlayCreatedAsync();
        _pendingImagePath = null;
    }

    [RelayCommand]
    private async Task DeleteTaskAsync(TaskItem task)
    {
        try
        {
            await _soundService.PlayClickAsync();
            if (task is null || !await Application.Current.MainPage.DisplayAlert("Eliminar", "¿Borrar esta tarea?", "Sí", "No")) return;

            await _databaseService.DeleteTaskAsync(task);
            Tasks.Remove(task);
            await _soundService.PlayDeletedAsync();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteHabitAsync(HabitItem habit)
    {
        try
        {
            await _soundService.PlayClickAsync();
            if (habit is null || !await Application.Current.MainPage.DisplayAlert("Eliminar", "¿Borrar este hábito?", "Sí", "No")) return;

            await _databaseService.DeleteHabitAsync(habit);
            Habits.Remove(habit);
            await _soundService.PlayDeletedAsync();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteRewardAsync(RewardItem reward)
    {
        try
        {
            await _soundService.PlayClickAsync();

            // 🛡️ ESCUDO ACTIVADO: Si es de sistema, salimos de aquí sin hacer nada
            if (reward == null || reward.IsSystemReward) return;

            if (!await Application.Current.MainPage.DisplayAlert("Eliminar", "¿Borrar este capricho?", "Sí", "No")) return;

            await _databaseService.DeleteRewardAsync(reward);
            Rewards.Remove(reward);
            await _soundService.PlayDeletedAsync();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task CompleteTaskAsync(TaskItem task)
    {
        if (task is null || task.IsCompleted) return;

        if (task.DueDateTime <= DateTime.Now)
        {
            task.IsFailed = true;
            await _databaseService.SaveTaskAsync(task);
            CurrentUserProfile.Health -= 10;
            if (CurrentUserProfile.Health <= 0) await ApplyGameOverAsync();
            else { await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile)); }

            await _soundService.PlayFailAsync();
            await NotifyMissionFailedAsync(task.Title);
            await LoadTasksAsync(); // <-- Al recargar aquí, la tarea fallida se oculta
            return;
        }

        task.IsCompleted = true;
        await _databaseService.SaveTaskAsync(task);
        await AddExperienceAsync(10, task.RewardCoins);
        await LoadTasksAsync(); // <-- Al recargar aquí, la tarea completada se oculta
        await _soundService.PlayTaDaAsync();
        await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task ExecuteHabitAsync(HabitItem habit)
    {
        if (habit is null) return;

        var today = DateTime.Today;
        var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
        var activeDays = (habit.ActiveDaysCsv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!activeDays.Contains(todayCode)) { await Application.Current.MainPage.DisplayAlert("Aviso", "No toca hoy.", "OK"); return; }
        if (habit.LastCompletedDate.Date == today) { await Application.Current.MainPage.DisplayAlert("Aviso", "Ya está completado.", "OK"); return; }

        if (DateTime.Now > today.Add(habit.ScheduledTime))
        {
            CurrentUserProfile.Health -= 5;
            habit.LastPenaltyDate = today;
            await _databaseService.SaveHabitAsync(habit);
            if (CurrentUserProfile.Health <= 0) await ApplyGameOverAsync();
            else { await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile)); }

            await _soundService.PlayFailAsync();
            return;
        }

        await AddExperienceAsync(5, habit.RewardCoins);
        habit.LastCompletedDate = today;
        await _databaseService.SaveHabitAsync(habit);
        await _soundService.PlayTaDaAsync();
        await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task BuyRewardAsync(RewardItem reward)
    {
        if (reward is null) return;

        if (CurrentUserProfile.Coins >= reward.Cost)
        {
            CurrentUserProfile.Coins -= reward.Cost;
            if (reward.HealthRestore > 0) CurrentUserProfile.Health = Math.Min(CurrentUserProfile.MaxHealth, CurrentUserProfile.Health + reward.HealthRestore);

            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));
            await _soundService.PlayRewardBoughtAsync();
            await Application.Current.MainPage.DisplayAlert("¡Premio!", "Disfruta de tu recompensa", "OK");
            return;
        }
        await _soundService.PlayFailAsync();
        await Application.Current.MainPage.DisplayAlert("Aviso", "No tienes suficientes monedas", "OK");
    }
}