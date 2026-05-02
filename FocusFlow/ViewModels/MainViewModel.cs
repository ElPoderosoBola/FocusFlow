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

    // --- EL NUEVO SISTEMA DE HOLOGRAMAS (ALERTAS) ---
    [ObservableProperty] private bool isAlertVisible;
    [ObservableProperty] private string alertTitle;
    [ObservableProperty] private string alertMessage;
    [ObservableProperty] private bool isConfirmMode;
    private TaskCompletionSource<bool> _alertTcs;

    // VARIABLES PARA TAREAS
    [ObservableProperty] private bool isCreatingTask;
    [ObservableProperty] private string newTaskTitle;
    [ObservableProperty] private string newTaskRewardCoinsText;
    [ObservableProperty] private DateTime newTaskDate;
    [ObservableProperty] private TimeSpan newTaskTime;

    // VARIABLES PARA HÁBITOS
    [ObservableProperty] private bool isCreatingHabit;
    [ObservableProperty] private string newHabitTitle;
    [ObservableProperty] private string newHabitRewardCoinsText;
    [ObservableProperty] private TimeSpan newHabitTime;
    [ObservableProperty] private string newHabitActiveDays;

    // VARIABLES PARA CAPRICHOS (¡NUEVO!)
    [ObservableProperty] private bool isCreatingReward;
    [ObservableProperty] private string newRewardTitle;
    [ObservableProperty] private string newRewardCostText;

    [ObservableProperty] private ObservableCollection<TaskItem> tasks = new();
    [ObservableProperty] private ObservableCollection<HabitItem> habits = new();
    [ObservableProperty] private ObservableCollection<RewardItem> rewards = new();
    [ObservableProperty] private UserProfile currentUserProfile = new() { Id = 1, Level = 1, CurrentXP = 0, Coins = 0, Health = 50, MaxHealth = 50 };

    public MainViewModel(DatabaseService databaseService, SoundService soundService)
    {
        _databaseService = databaseService;
        _soundService = soundService;

        _timer = Application.Current.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(1);
        _timer.Tick += async (_, _) => await CheckMissionDeadlinesAsync();
    }

    // --- CREADOR DE HOLOGRAMAS (Reemplaza a DisplayAlert) ---
    private async Task<bool> ShowAeroAlert(string title, string message, bool isConfirm = false)
    {
        AlertTitle = title;
        AlertMessage = message;
        IsConfirmMode = isConfirm;
        IsAlertVisible = true;
        _alertTcs = new TaskCompletionSource<bool>();
        return await _alertTcs.Task;
    }

    [RelayCommand]
    private async Task CloseAlertAsync(string result)
    {
        await _soundService.PlayClickAsync();
        IsAlertVisible = false;
        _alertTcs?.TrySetResult(result == "Yes");
    }
    // --------------------------------------------------------

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
        await ShowAeroAlert("¡Caíste en batalla!", "Has perdido toda tu salud. Pierdes 1 Nivel y 50 monedas.");

        CurrentUserProfile.Level = Math.Max(0, CurrentUserProfile.Level - 1);
        CurrentUserProfile.Coins = Math.Max(0, CurrentUserProfile.Coins - 50);
        CurrentUserProfile.CurrentXP = 0;
        CurrentUserProfile.Health = CurrentUserProfile.MaxHealth;

        CurrentUserProfile.TimesDied++; // 💀 ANOTAMOS LA MUERTE

        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));
        await CheckAchievementsAsync(); // Comprobamos si le toca el logro
    }

    private async Task NotifyMissionFailedAsync(string missionTitle)
    {
        var request = new NotificationRequest
        {
            NotificationId = 3000 + Random.Shared.Next(1, 999),
            Title = "Misión fallida",
            Description = $"No has completado {missionTitle}",
            Schedule = new NotificationRequestSchedule { NotifyTime = DateTime.Now.AddSeconds(1) }
        };
        await LocalNotificationCenter.Current.Show(request);
    }

    private async Task ScheduleDailyReminderAsync()
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
            await LocalNotificationCenter.Current.RequestNotificationPermission();

        var request = new NotificationRequest
        {
            NotificationId = 100,
            Title = "¡Despierta, Héroe!",
            Description = "Tus misiones te esperan. ¡No pierdas tu racha!",
            Schedule = new NotificationRequestSchedule { NotifyTime = DateTime.Today.AddDays(1).AddHours(9), RepeatType = NotificationRepeat.Daily }
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
                        Description = $"Solo queda media hora para {habit.Title}",
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
            await ShowAeroAlert("Fallida", $"No has completado {task.Title}");
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        }

        foreach (var habit in Habits)
        {
            var today = DateTime.Today;
            var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
            var activeDays = (habit.ActiveDaysCsv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!activeDays.Contains(todayCode)) continue;

            var habitDeadline = today.Add(habit.ScheduledTime);
            if (habit.LastCompletedDate.Date != today && habit.LastPenaltyDate.Date != today && habitDeadline <= now)
            {
                CurrentUserProfile.Health -= 5;
                habit.LastPenaltyDate = today;
                await _databaseService.SaveHabitAsync(habit);

                await _soundService.PlayFailAsync();
                await NotifyMissionFailedAsync(habit.Title);
                await ShowAeroAlert("Fallida", $"No has completado {habit.Title}");
                await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            }
        }

        if (CurrentUserProfile.Health <= 0) await ApplyGameOverAsync();
    }

    public async Task LoadTasksAsync() { var allTasks = await _databaseService.GetTasksAsync(_currentUserId); Tasks = new ObservableCollection<TaskItem>(allTasks.Where(t => !t.IsCompleted && !t.IsFailed)); }
    public async Task LoadHabitsAsync() { Habits = new ObservableCollection<HabitItem>(await _databaseService.GetHabitsAsync(_currentUserId)); }
    public async Task LoadRewardsAsync() { Rewards = new ObservableCollection<RewardItem>(await _databaseService.GetRewardsAsync(_currentUserId)); }

    private async Task AddExperienceAsync(int amount, int coinsToAdd = 0)
    {
        CurrentUserProfile.CurrentXP += amount;
        CurrentUserProfile.Coins += coinsToAdd;
        CurrentUserProfile.TotalCoinsEarned += coinsToAdd; // 💰 ANOTAMOS GANANCIA HISTÓRICA

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

        // Función ayudante interna para no repetir código
        async Task Unlock(string title, string desc)
        {
            var ach = achievements.FirstOrDefault(a => a.Title == title);
            if (ach is not null && !ach.IsUnlocked)
            {
                ach.IsUnlocked = true; CurrentUserProfile.Coins += 20; CurrentUserProfile.TotalCoinsEarned += 20;
                await _databaseService.UpdateAchievementAsync(ach);
                unlockedAny = true; unlockedTitles += $"🏅 {title} ({desc})\n";
            }
        }

        if (CurrentUserProfile.Level >= 2) await Unlock("Aprendiz", "Nivel 2");
        if (CurrentUserProfile.Coins >= 50) await Unlock("Acaparador", "Ahorrar 50 monedas");
        if (CurrentUserProfile.CurrentXP > 0 || Tasks.Any(t => t.IsCompleted) || Habits.Any(h => h.LastCompletedDate.Date == DateTime.Today)) await Unlock("Primeros Pasos", "¡Arrancaste!");

        // LOS NUEVOS
        if (CurrentUserProfile.TotalCoinsEarned >= 5000) await Unlock("Atiza como cotiza", "Ganaste 5000 monedas históricas");
        if (CurrentUserProfile.Coins >= 3000) await Unlock("El lobo de FocusFlow Street", "Acumulaste 3000 monedas");
        if (CurrentUserProfile.UsedHealReward) await Unlock("Paracetamol y a correr", "Usaste poción de salud");
        if (CurrentUserProfile.TimesDied >= 1) await Unlock("Y resucitó al tercer día...", "Caíste en batalla");
        if (Habits.Count >= 5) await Unlock("John Multitasking", "5 Hábitos creados");
        if (CurrentUserProfile.TotalRewardsBought >= 5) await Unlock("Quien bien trabaja, bien descansa", "5 Recompensas compradas");
        if (CurrentUserProfile.TotalRewardsBought >= 50) await Unlock("Ni en Silicon Valley", "50 Recompensas compradas");
        if (CurrentUserProfile.TotalTasksCompleted >= 30) await Unlock("Autónomo rutinal", "30 Misiones completadas");

        if (unlockedAny)
        {
            await _soundService.PlayTaDaAsync();
            await ShowAeroAlert("¡NUEVO LOGRO DESBLOQUEADO!", $"Has conseguido:\n\n{unlockedTitles}\n¡Y te llevas monedas extra de premio!");
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));
        }
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        await _soundService.PlayClickAsync();
        var photo = await MediaPicker.Default.PickPhotoAsync();
        if (photo != null) _pendingImagePath = photo.FullPath;
    }

    // --- TAREAS ---
    [RelayCommand]
    private async Task AddNewTaskAsync()
    {
        await _soundService.PlayClickAsync();
        _pendingImagePath = null;
        NewTaskTitle = "";
        NewTaskRewardCoinsText = "10";
        NewTaskDate = DateTime.Today;
        NewTaskTime = DateTime.Now.AddHours(1).TimeOfDay;
        IsCreatingTask = true;
    }

    [RelayCommand]
    private async Task SaveTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle) || !int.TryParse(NewTaskRewardCoinsText, out int rewardCoins) || rewardCoins < 0)
        {
            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Error", "Revisa el nombre y las monedas.");
            return;
        }

        var newTask = new TaskItem { UserId = _currentUserId, Title = NewTaskTitle.Trim(), DueDateTime = NewTaskDate.Date.Add(NewTaskTime), RewardCoins = rewardCoins, IsCompleted = false, IsFailed = false, ImagePath = _pendingImagePath };
        await _databaseService.SaveTaskAsync(newTask);
        Tasks.Add(newTask);
        await _soundService.PlayCreatedAsync();
        _pendingImagePath = null;
        IsCreatingTask = false;
    }

    [RelayCommand]
    private async Task CancelTaskAsync() { await _soundService.PlayClickAsync(); IsCreatingTask = false; _pendingImagePath = null; }

    // --- HÁBITOS ---
    [RelayCommand]
    private async Task AddNewHabitAsync()
    {
        await _soundService.PlayClickAsync();
        _pendingImagePath = null;
        NewHabitTitle = "";
        NewHabitRewardCoinsText = "5";
        NewHabitTime = DateTime.Now.AddHours(1).TimeOfDay;
        IsCreatingHabit = true;
    }

    [RelayCommand]
    private async Task SaveHabitAsync()
    {
        if (string.IsNullOrWhiteSpace(NewHabitTitle) || !int.TryParse(NewHabitRewardCoinsText, out int rewardCoins) || rewardCoins < 0)
        {
            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Error", "Revisa el nombre y las monedas.");
            return;
        }

        var dayTokens = string.Join(",", WeekDays.Where(d => d.IsSelected).Select(d => d.NumberValue));
        if (string.IsNullOrEmpty(dayTokens))
        {
            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Error", "Debes seleccionar al menos un día en las burbujas.");
            return;
        }

        var newHabit = new HabitItem { UserId = _currentUserId, Title = NewHabitTitle.Trim(), ScheduledTime = NewHabitTime, ActiveDaysCsv = dayTokens, RewardCoins = rewardCoins, IsPositive = true, ImagePath = _pendingImagePath };
        await _databaseService.SaveHabitAsync(newHabit);
        Habits.Add(newHabit);
        await _soundService.PlayCreatedAsync();
        _pendingImagePath = null;
        IsCreatingHabit = false;

        // 🏆 ¡EL ÁRBITRO AHORA MIRA! Comprobamos si nos merecemos el logro de los 5 hábitos
        await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task CancelHabitAsync() { await _soundService.PlayClickAsync(); IsCreatingHabit = false; _pendingImagePath = null; }

    // --- CAPRICHOS ---
    [RelayCommand]
    private async Task AddNewRewardAsync()
    {
        await _soundService.PlayClickAsync();
        var maxCustomRewards = 15 + (CurrentUserProfile.Level * 5);
        if (Rewards.Count(r => !r.IsSystemReward) >= maxCustomRewards)
        {
            await ShowAeroAlert("Mochila Llena", $"Alcanzaste el tope de caprichos ({maxCustomRewards}).");
            return;
        }

        _pendingImagePath = null;
        NewRewardTitle = "";
        NewRewardCostText = "50";
        IsCreatingReward = true;
    }

    [RelayCommand]
    private async Task SaveRewardAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRewardTitle) || !int.TryParse(NewRewardCostText, out int cost) || cost < 0)
        {
            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Error", "Revisa el nombre y el coste del capricho.");
            return;
        }

        var newReward = new RewardItem { UserId = _currentUserId, Title = NewRewardTitle.Trim(), Cost = cost, ImagePath = _pendingImagePath };
        await _databaseService.SaveRewardAsync(newReward);
        Rewards.Add(newReward);
        await _soundService.PlayCreatedAsync();
        _pendingImagePath = null;
        IsCreatingReward = false;
    }

    [RelayCommand]
    private async Task CancelRewardAsync() { await _soundService.PlayClickAsync(); IsCreatingReward = false; _pendingImagePath = null; }


    // --- ACCIONES ---
    [RelayCommand]
    private async Task DeleteTaskAsync(TaskItem task)
    {
        try { await _soundService.PlayClickAsync(); if (task is null || !await ShowAeroAlert("Eliminar", "¿Borrar esta tarea?", true)) return; await _databaseService.DeleteTaskAsync(task); Tasks.Remove(task); await _soundService.PlayDeletedAsync(); }
        catch (Exception ex) { await ShowAeroAlert("Error", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteHabitAsync(HabitItem habit)
    {
        try { await _soundService.PlayClickAsync(); if (habit is null || !await ShowAeroAlert("Eliminar", "¿Borrar este hábito?", true)) return; await _databaseService.DeleteHabitAsync(habit); Habits.Remove(habit); await _soundService.PlayDeletedAsync(); }
        catch (Exception ex) { await ShowAeroAlert("Error", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteRewardAsync(RewardItem reward)
    {
        try { await _soundService.PlayClickAsync(); if (reward is null || reward.IsSystemReward) return; if (!await ShowAeroAlert("Eliminar", "¿Borrar este capricho?", true)) return; await _databaseService.DeleteRewardAsync(reward); Rewards.Remove(reward); await _soundService.PlayDeletedAsync(); }
        catch (Exception ex) { await ShowAeroAlert("Error", ex.Message); }
    }

    [RelayCommand]
    private async Task CompleteTaskAsync(TaskItem task)
    {
        if (task is null || task.IsCompleted) return;
        if (task.DueDateTime <= DateTime.Now)
        {
            task.IsFailed = true; await _databaseService.SaveTaskAsync(task); CurrentUserProfile.Health -= 10;
            if (CurrentUserProfile.Health <= 0) await ApplyGameOverAsync(); else { await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile)); }
            await _soundService.PlayFailAsync(); await NotifyMissionFailedAsync(task.Title); await LoadTasksAsync(); return;
        }

        task.IsCompleted = true; await _databaseService.SaveTaskAsync(task);
        CurrentUserProfile.TotalTasksCompleted++; // 🗡️ ANOTAMOS MISIÓN CUMPLIDA

        await AddExperienceAsync(10, task.RewardCoins); await LoadTasksAsync(); await _soundService.PlayTaDaAsync(); await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task ExecuteHabitAsync(HabitItem habit)
    {
        if (habit is null) return;
        var today = DateTime.Today; var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
        var activeDays = (habit.ActiveDaysCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!activeDays.Contains(todayCode)) { await ShowAeroAlert("Aviso", "No toca hoy."); return; }
        if (habit.LastCompletedDate.Date == today) { await ShowAeroAlert("Aviso", "Ya está completado hoy."); return; }
        if (DateTime.Now > today.Add(habit.ScheduledTime))
        {
            CurrentUserProfile.Health -= 5; habit.LastPenaltyDate = today; await _databaseService.SaveHabitAsync(habit);
            if (CurrentUserProfile.Health <= 0) await ApplyGameOverAsync(); else { await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile)); }
            await _soundService.PlayFailAsync(); return;
        }
        await AddExperienceAsync(5, habit.RewardCoins); habit.LastCompletedDate = today; await _databaseService.SaveHabitAsync(habit); await _soundService.PlayTaDaAsync(); await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task BuyRewardAsync(RewardItem reward)
    {
        if (reward is null) return;
        if (CurrentUserProfile.Coins >= reward.Cost)
        {
            CurrentUserProfile.Coins -= reward.Cost;
            CurrentUserProfile.TotalRewardsBought++; // 🛍️ ANOTAMOS COMPRA

            if (reward.HealthRestore > 0)
            {
                CurrentUserProfile.Health = Math.Min(CurrentUserProfile.MaxHealth, CurrentUserProfile.Health + reward.HealthRestore);
                CurrentUserProfile.UsedHealReward = true; // 💊 ANOTAMOS USO DE POCIÓN
            }

            await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile));
            await _soundService.PlayRewardBoughtAsync(); await ShowAeroAlert("¡Premio!", "Disfruta de tu recompensa.");
            await CheckAchievementsAsync(); // Comprobamos logros
            return;
        }
        await _soundService.PlayFailAsync(); await ShowAeroAlert("Aviso", "No tienes suficientes monedas.");
    }

    [ObservableProperty]
    private ObservableCollection<DayBubble> weekDays = new()
    {
        new DayBubble { Name = "L", NumberValue = "1", IsSelected = false },
        new DayBubble { Name = "M", NumberValue = "2", IsSelected = false },
        new DayBubble { Name = "X", NumberValue = "3", IsSelected = false },
        new DayBubble { Name = "J", NumberValue = "4", IsSelected = false },
        new DayBubble { Name = "V", NumberValue = "5", IsSelected = false },
        new DayBubble { Name = "S", NumberValue = "6", IsSelected = false },
        new DayBubble { Name = "D", NumberValue = "0", IsSelected = false }
    };

    [RelayCommand]
    private void ToggleDay(DayBubble bubble) { if (bubble != null) bubble.IsSelected = !bubble.IsSelected; }
}