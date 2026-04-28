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

    // Lista observable para que la UI se actualice cuando cambien las tareas.
    [ObservableProperty]
    private ObservableCollection<TaskItem> tasks = new();

    // Lista observable de hábitos.
    [ObservableProperty]
    private ObservableCollection<HabitItem> habits = new();

    // Lista observable de recompensas.
    [ObservableProperty]
    private ObservableCollection<RewardItem> rewards = new();

    // Perfil actual del usuario (nivel y experiencia persistidos).
    [ObservableProperty]
    private UserProfile currentUserProfile = new() { Id = 1, Level = 1, CurrentXP = 0, Coins = 0, Health = 50, MaxHealth = 50 };

    public MainViewModel(DatabaseService databaseService, SoundService soundService)
    {
        _databaseService = databaseService;
        _soundService = soundService;

        // Timer interno para revisar vencimientos cada minuto.
        _timer = Application.Current.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(1);
        _timer.Tick += async (_, _) => await CheckMissionDeadlinesAsync();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        // Carga el último usuario activo desde sesión local.
        var session = await _databaseService.GetUserSessionAsync();
        _currentUserId = session.CurrentUserId;

        // Si no hay sesión válida, salimos para evitar cargar datos sin login.
        if (_currentUserId == 0)
        {
            return;
        }

        // Carga inicial de datos cuando el ViewModel está listo para usarse.
        CurrentUserProfile = await _databaseService.GetUserProfileAsync(_currentUserId);
        await LoadTasksAsync();
        await LoadHabitsAsync();

        // Guarda la nueva fecha de acceso del usuario.
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
        await Application.Current.MainPage.DisplayAlert(
            "¡Caíste en batalla!",
            "Has perdido toda tu salud. Pierdes 1 Nivel y 50 monedas.",
            "Resucitar");

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
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = DateTime.Today.AddDays(1).AddHours(9),
                RepeatType = NotificationRepeat.Daily
            }
        };

        await LocalNotificationCenter.Current.Show(request);

        // Programa recordatorios de 30 minutos para tareas y hábitos activos.
        await ScheduleUpcomingMissionNotificationsAsync();
    }

    private async Task ScheduleUpcomingMissionNotificationsAsync()
    {
        var now = DateTime.Now;

        foreach (var task in Tasks.Where(t => !t.IsCompleted && !t.IsFailed))
        {
            var notifyTime = task.DueDateTime.AddMinutes(-30);
            if (notifyTime <= now)
            {
                continue;
            }

            var taskRequest = new NotificationRequest
            {
                NotificationId = 1000 + task.Id,
                Title = "Aviso de misión",
                Description = $"Solo queda media hora para completar {task.Title} ¿Lo acabaste o estás con ello?",
                Schedule = new NotificationRequestSchedule { NotifyTime = notifyTime }
            };

            await LocalNotificationCenter.Current.Show(taskRequest);
        }

        foreach (var habit in Habits)
        {
            if (string.IsNullOrWhiteSpace(habit.ActiveDaysCsv))
            {
                continue;
            }

            var today = DateTime.Today;
            var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
            var activeDays = habit.ActiveDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!activeDays.Contains(todayCode))
            {
                continue;
            }

            var habitDateTime = today.Add(habit.ScheduledTime);
            var notifyTime = habitDateTime.AddMinutes(-30);
            if (notifyTime <= now)
            {
                continue;
            }

            var habitRequest = new NotificationRequest
            {
                NotificationId = 2000 + habit.Id,
                Title = "Aviso de misión",
                Description = $"Solo queda media hora para completar {habit.Title} ¿Lo acabaste o estás con ello?",
                Schedule = new NotificationRequestSchedule { NotifyTime = notifyTime }
            };

            await LocalNotificationCenter.Current.Show(habitRequest);
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
            await Application.Current.MainPage.DisplayAlert("Misión fallida", $"Misión fallida. No has completado {task.Title}", "OK");
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        }

        foreach (var habit in Habits)
        {
            var today = DateTime.Today;
            var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
            var activeDays = (habit.ActiveDaysCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!activeDays.Contains(todayCode))
            {
                continue;
            }

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
                await Application.Current.MainPage.DisplayAlert("Misión fallida", $"Misión fallida. No has completado {habit.Title}", "OK");
                await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            }
        }

        if (CurrentUserProfile.Health <= 0)
        {
            await ApplyGameOverAsync();
        }
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

    public async Task LoadRewardsAsync()
    {
        var rewardList = await _databaseService.GetRewardsAsync(_currentUserId);
        Rewards = new ObservableCollection<RewardItem>(rewardList);
    }

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

    private static int GetXpNeededForLevel(int level)
    {
        return 50 + (level * 5);
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
        var hasProgress = CurrentUserProfile.CurrentXP > 0 || Tasks.Any(t => t.IsCompleted) || Habits.Any(h => h.LastCompletedDate.Date == DateTime.Today);
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
        await _soundService.PlayClickAsync();

        var title = await Application.Current.MainPage.DisplayPromptAsync("Nuevo Hábito", "Nombre del hábito:");
        if (string.IsNullOrWhiteSpace(title)) return;

        var rewardText = await Application.Current.MainPage.DisplayPromptAsync("Recompensa", "¿Cuántas monedas da este hábito?");
        if (string.IsNullOrWhiteSpace(rewardText) || !int.TryParse(rewardText, out var rewardCoins) || rewardCoins < 0)
        {
            await Application.Current.MainPage.DisplayAlert("Hábito", "Las monedas no son válidas.", "OK");
            return;
        }

        var timeText = await Application.Current.MainPage.DisplayPromptAsync("Hora", "Hora del hábito (HH:mm):");
        if (string.IsNullOrWhiteSpace(timeText) ||
            !TimeSpan.TryParseExact(timeText, "hh\\:mm", CultureInfo.InvariantCulture, out var scheduledTime))
        {
            await Application.Current.MainPage.DisplayAlert("Hábito", "La hora no tiene formato HH:mm.", "OK");
            return;
        }

        var daysText = await Application.Current.MainPage.DisplayPromptAsync(
            "Días activos",
            "Escribe días por número (0=Dom .. 6=Sáb), separados por coma. Ej: 1,3,5");

        if (string.IsNullOrWhiteSpace(daysText))
        {
            await Application.Current.MainPage.DisplayAlert("Hábito", "Debes indicar al menos un día activo.", "OK");
            return;
        }

        var dayTokens = daysText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => int.TryParse(x, out var day) && day >= 0 && day <= 6)
            .Distinct()
            .ToArray();

        if (dayTokens.Length == 0)
        {
            await Application.Current.MainPage.DisplayAlert("Hábito", "No hay días válidos (usa valores de 0 a 6).", "OK");
            return;
        }

        var newHabit = new HabitItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            ScheduledTime = scheduledTime,
            ActiveDaysCsv = string.Join(',', dayTokens),
            RewardCoins = rewardCoins,
            LastCompletedDate = DateTime.MinValue,
            LastPenaltyDate = DateTime.MinValue,
            IsPositive = true,
            IsNegative = false,
            ImagePath = _pendingImagePath
        };

        await _databaseService.SaveHabitAsync(newHabit);
        Habits.Add(newHabit);
        await _soundService.PlaySuccessAsync();
        _pendingImagePath = null;
    }

    [RelayCommand]
    private async Task AddNewTaskAsync()
    {
        await _soundService.PlayClickAsync();

        var title = await Application.Current.MainPage.DisplayPromptAsync("Nueva Tarea", "Nombre de la tarea:");
        if (string.IsNullOrWhiteSpace(title)) return;

        var minutesText = await Application.Current.MainPage.DisplayPromptAsync("Duración", "Minutos estimados (ej: 25):", initialValue: "25");
        if (string.IsNullOrWhiteSpace(minutesText) || !int.TryParse(minutesText, out var estimatedMinutes) || estimatedMinutes <= 0)
        {
            await Application.Current.MainPage.DisplayAlert("Tarea", "Los minutos estimados no son válidos.", "OK");
            return;
        }

        var rewardText = await Application.Current.MainPage.DisplayPromptAsync("Recompensa", "¿Cuántas monedas da esta tarea?", initialValue: "10");
        if (string.IsNullOrWhiteSpace(rewardText) || !int.TryParse(rewardText, out var rewardCoins) || rewardCoins < 0)
        {
            await Application.Current.MainPage.DisplayAlert("Tarea", "Las monedas no son válidas.", "OK");
            return;
        }

        var dateText = await Application.Current.MainPage.DisplayPromptAsync("Fecha límite", "Fecha de vencimiento (yyyy-MM-dd):", initialValue: DateTime.Today.ToString("yyyy-MM-dd"));
        var timeText = await Application.Current.MainPage.DisplayPromptAsync("Hora límite", "Hora de vencimiento (HH:mm):", initialValue: DateTime.Now.AddHours(1).ToString("HH:mm"));

        if (string.IsNullOrWhiteSpace(dateText) || string.IsNullOrWhiteSpace(timeText) ||
            !DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueDate) ||
            !TimeSpan.TryParseExact(timeText, "hh\\:mm", CultureInfo.InvariantCulture, out var dueTime))
        {
            await Application.Current.MainPage.DisplayAlert("Tarea", "Fecha u hora no válidas. Usa yyyy-MM-dd y HH:mm.", "OK");
            return;
        }

        var dueDateTime = dueDate.Date.Add(dueTime);

        var newTask = new TaskItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            EstimatedMinutes = estimatedMinutes,
            DueDateTime = dueDateTime,
            RewardCoins = rewardCoins,
            IsCompleted = false,
            IsFailed = false,
            ImagePath = _pendingImagePath
        };

        await _databaseService.SaveTaskAsync(newTask);
        Tasks.Add(newTask);
        await _soundService.PlaySuccessAsync();
        _pendingImagePath = null;
    }

    [RelayCommand]
    private async Task AddNewRewardAsync()
    {
        await _soundService.PlayClickAsync();

        var maxCustomRewards = 15 + (CurrentUserProfile.Level * 5);
        var currentCustomRewards = Rewards.Count(r => !r.IsSystemReward);

        if (currentCustomRewards >= maxCustomRewards)
        {
            await Application.Current.MainPage.DisplayAlert(
                "Recompensas",
                $"Has alcanzado el límite de recompensas custom ({maxCustomRewards}).",
                "OK");
            return;
        }

        var title = await Application.Current.MainPage.DisplayPromptAsync("Nueva Recompensa", "¿Qué recompensa quieres añadir?");
        if (string.IsNullOrWhiteSpace(title)) return;

        var costText = await Application.Current.MainPage.DisplayPromptAsync("Coste de Recompensa", "¿Cuántas monedas cuesta?");
        if (string.IsNullOrWhiteSpace(costText) || !int.TryParse(costText, out var cost) || cost < 0) return;

        var newReward = new RewardItem
        {
            UserId = _currentUserId,
            Title = title.Trim(),
            Cost = cost,
            ImagePath = _pendingImagePath,
            IsSystemReward = false,
            HealthRestore = 0
        };

        await _databaseService.SaveRewardAsync(newReward);
        Rewards.Add(newReward);
        await _soundService.PlaySuccessAsync();
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
            await _soundService.PlayClickAsync();
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
            await _soundService.PlayClickAsync();
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
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));

            await NotifyMissionFailedAsync(task.Title);
            await Application.Current.MainPage.DisplayAlert("Misión fallida", $"Misión fallida. No has completado {task.Title}", "OK");

            if (CurrentUserProfile.Health <= 0)
            {
                await ApplyGameOverAsync();
            }

            await LoadTasksAsync();
            return;
        }

        task.IsCompleted = true;
        task.CompletedAt = DateTime.Now;
        await _databaseService.SaveTaskAsync(task);

        await AddExperienceAsync(10, task.RewardCoins);
        await LoadTasksAsync();
        await _soundService.PlaySuccessAsync();
        await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task ExecuteHabitAsync(HabitItem habit)
    {
        if (habit is null) return;

        var today = DateTime.Today;
        var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
        var activeDays = (habit.ActiveDaysCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!activeDays.Contains(todayCode))
        {
            await Application.Current.MainPage.DisplayAlert("Hábito", "Este hábito no toca hoy.", "OK");
            return;
        }

        if (habit.LastCompletedDate.Date == today)
        {
            await Application.Current.MainPage.DisplayAlert("Hábito", "Este hábito ya está completado hoy.", "OK");
            return;
        }

        var habitDeadline = today.Add(habit.ScheduledTime);
        if (DateTime.Now > habitDeadline)
        {
            CurrentUserProfile.Health -= 5;
            habit.LastPenaltyDate = today;
            await _databaseService.SaveHabitAsync(habit);
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));

            await NotifyMissionFailedAsync(habit.Title);
            await Application.Current.MainPage.DisplayAlert("Misión fallida", $"Misión fallida. No has completado {habit.Title}", "OK");

            if (CurrentUserProfile.Health <= 0)
            {
                await ApplyGameOverAsync();
            }

            return;
        }

        if (habit.IsPositive)
        {
            await AddExperienceAsync(5, habit.RewardCoins);
            habit.LastCompletedDate = today;
        }

        if (habit.IsNegative)
        {
            CurrentUserProfile.Health -= 5;

            if (CurrentUserProfile.Health <= 0)
            {
                await ApplyGameOverAsync();
            }
        }

        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));

        await _databaseService.SaveHabitAsync(habit);
        await _soundService.PlaySuccessAsync();
        await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task BuyRewardAsync(RewardItem reward)
    {
        if (reward is null) return;

        await _soundService.PlayClickAsync();

        if (CurrentUserProfile.Coins >= reward.Cost)
        {
            CurrentUserProfile.Coins -= reward.Cost;

            if (reward.HealthRestore > 0)
            {
                CurrentUserProfile.Health = Math.Min(CurrentUserProfile.MaxHealth, CurrentUserProfile.Health + reward.HealthRestore);
            }

            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));

            await _soundService.PlaySuccessAsync();
            await Application.Current.MainPage.DisplayAlert("Recompensas", "¡Premio comprado! Disfruta de tu recompensa", "OK");
            return;
        }

        await _soundService.PlayFailAsync();
        await Application.Current.MainPage.DisplayAlert("Recompensas", "No tienes suficientes monedas", "OK");
    }
}