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

    [ObservableProperty] private bool isAlertVisible;
    [ObservableProperty] private string alertTitle;
    [ObservableProperty] private string alertMessage;
    [ObservableProperty] private bool isConfirmMode;
    private TaskCompletionSource<bool> _alertTcs;

    [ObservableProperty] private bool isCreatingTask;
    [ObservableProperty] private string newTaskTitle;
    [ObservableProperty] private string newTaskRewardCoinsText;
    [ObservableProperty] private DateTime newTaskDate;
    [ObservableProperty] private TimeSpan newTaskTime;

    [ObservableProperty] private bool isCreatingHabit;
    [ObservableProperty] private string newHabitTitle;
    [ObservableProperty] private string newHabitRewardCoinsText;
    [ObservableProperty] private TimeSpan newHabitTime;
    [ObservableProperty] private string newHabitActiveDays;

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

    private async Task<bool> ShowAeroAlert(string title, string message, bool isConfirm = false)
    {
        AlertTitle = title; AlertMessage = message; IsConfirmMode = isConfirm; IsAlertVisible = true;
        _alertTcs = new TaskCompletionSource<bool>();
        return await _alertTcs.Task;
    }

    [RelayCommand]
    private async Task CloseAlertAsync(string result)
    {
        await _soundService.PlayClickAsync(); IsAlertVisible = false; _alertTcs?.TrySetResult(result == "Yes");
    }

    public async Task InitializeAsync()
    {

        if (!_isInitialized)
        {
            var session = await _databaseService.GetUserSessionAsync();
            _currentUserId = session.CurrentUserId;
            if (_currentUserId == 0) return;

            CurrentUserProfile = await _databaseService.GetUserProfileAsync(_currentUserId);
            await LoadTasksAsync();
            await LoadHabitsAsync();
            await LoadRewardsAsync();

            CurrentUserProfile.LastLoginDate = DateTime.Today;
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));

            await ScheduleDailyReminderAsync();
            _timer.Start();

            _isInitialized = true; 
        }


        await CheckMissionDeadlinesAsync();
    }


    private async Task ScheduleTaskNotifications(TaskItem task)
    {
        var now = DateTime.Now;
        var notifyTime = task.DueDateTime.AddMinutes(-30);


        if (notifyTime <= now && task.DueDateTime > now) notifyTime = now.AddSeconds(3);

        if (notifyTime > now || (notifyTime <= now && task.DueDateTime > now))
        {
            await LocalNotificationCenter.Current.Show(new NotificationRequest
            {
                NotificationId = 1000 + task.Id,
                Title = "¡Aviso de misión!",
                Description = $"Se acaba el tiempo para: {task.Title}",
                Schedule = new NotificationRequestSchedule { NotifyTime = notifyTime }
            });
        }


        if (task.DueDateTime > now)
        {
            await LocalNotificationCenter.Current.Show(new NotificationRequest
            {
                NotificationId = 3000 + task.Id,
                Title = "Misión Fallida 💀",
                Description = $"No terminaste '{task.Title}'. ¡Has perdido salud!",
                Schedule = new NotificationRequestSchedule { NotifyTime = task.DueDateTime }
            });
        }
    }

    private void CancelTaskNotifications(int taskId)
    {
        LocalNotificationCenter.Current.Cancel(1000 + taskId); 
        LocalNotificationCenter.Current.Cancel(3000 + taskId); 
    }

    private async Task ScheduleHabitNotifications(HabitItem habit)
    {
        var now = DateTime.Now; var today = DateTime.Today;
        var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
        var activeDays = (habit.ActiveDaysCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!activeDays.Contains(todayCode)) return;

        var habitDeadline = today.Add(habit.ScheduledTime);
        if (habit.LastCompletedDate.Date == today || habit.LastPenaltyDate.Date == today || habitDeadline <= now) return;

        var notifyTime = habitDeadline.AddMinutes(-30);


        if (notifyTime <= now) notifyTime = now.AddSeconds(3);

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = 2000 + habit.Id,
            Title = "¡Aviso de hábito!",
            Description = $"Tienes pendiente: {habit.Title}",
            Schedule = new NotificationRequestSchedule { NotifyTime = notifyTime }
        });


        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = 4000 + habit.Id,
            Title = "Hábito Fallido 💀",
            Description = $"No cumpliste '{habit.Title}'. ¡Has perdido salud!",
            Schedule = new NotificationRequestSchedule { NotifyTime = habitDeadline }
        });
    }

    private void CancelHabitNotifications(int habitId)
    {
        LocalNotificationCenter.Current.Cancel(2000 + habitId);
        LocalNotificationCenter.Current.Cancel(4000 + habitId);
    }
    // ------------------------------------------------

    private async Task ApplyGameOverAsync()
    {
        await ShowAeroAlert("¡Caíste en batalla!", "Has perdido toda tu salud. Pierdes 1 Nivel y 50 monedas.");
        CurrentUserProfile.Level = Math.Max(0, CurrentUserProfile.Level - 1);
        CurrentUserProfile.Coins = Math.Max(0, CurrentUserProfile.Coins - 50);
        CurrentUserProfile.CurrentXP = 0; CurrentUserProfile.Health = CurrentUserProfile.MaxHealth;
        CurrentUserProfile.TimesDied++;
        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));
        await CheckAchievementsAsync();
    }

    private async Task ScheduleDailyReminderAsync()
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
            await LocalNotificationCenter.Current.RequestNotificationPermission();

        var request = new NotificationRequest
        {
            NotificationId = 100,
            Title = "¡Despierta, Héroe!",
            Description = "Tus misiones te esperan.",
            Schedule = new NotificationRequestSchedule { NotifyTime = DateTime.Today.AddDays(1).AddHours(9), RepeatType = NotificationRepeat.Daily }
        };
        await LocalNotificationCenter.Current.Show(request);


        foreach (var task in Tasks.Where(t => !t.IsCompleted && !t.IsFailed)) await ScheduleTaskNotifications(task);
        foreach (var habit in Habits) await ScheduleHabitNotifications(habit);
    }

    private async Task CheckMissionDeadlinesAsync()
    {
        var now = DateTime.Now;
        bool needsUiUpdate = false; 


        foreach (var task in Tasks.Where(t => !t.IsCompleted && !t.IsFailed && t.DueDateTime <= now).ToList())
        {
            task.IsFailed = true;
            await _databaseService.SaveTaskAsync(task);

            CurrentUserProfile.Health -= 10;
            needsUiUpdate = true; 

            await _soundService.PlayFailAsync();
            await ShowAeroAlert("Misión Fallida 💀", $"Se acabó el tiempo para: {task.Title}");
        }


        foreach (var habit in Habits)
        {
            var today = DateTime.Today;
            var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
            var activeDays = (habit.ActiveDaysCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!activeDays.Contains(todayCode)) continue;

            var habitDeadline = today.Add(habit.ScheduledTime);
            if (habit.LastCompletedDate.Date != today && habit.LastPenaltyDate.Date != today && habitDeadline <= now)
            {
                CurrentUserProfile.Health -= 5;
                habit.LastPenaltyDate = today;
                await _databaseService.SaveHabitAsync(habit);
                needsUiUpdate = true; 

                await _soundService.PlayFailAsync();
                await ShowAeroAlert("Hábito Fallido 💀", $"No has completado: {habit.Title}");
            }
        }


        if (needsUiUpdate)
        {
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile)); 
            await LoadTasksAsync(); 
        }


        if (CurrentUserProfile.Health <= 0) await ApplyGameOverAsync();
    }

    public async Task LoadTasksAsync() { var allTasks = await _databaseService.GetTasksAsync(_currentUserId); Tasks = new ObservableCollection<TaskItem>(allTasks.Where(t => !t.IsCompleted && !t.IsFailed)); }
    public async Task LoadHabitsAsync() { Habits = new ObservableCollection<HabitItem>(await _databaseService.GetHabitsAsync(_currentUserId)); }
    public async Task LoadRewardsAsync() { Rewards = new ObservableCollection<RewardItem>(await _databaseService.GetRewardsAsync(_currentUserId)); }

    private async Task AddExperienceAsync(int amount, int coinsToAdd = 0)
    {
        CurrentUserProfile.CurrentXP += amount; CurrentUserProfile.Coins += coinsToAdd; CurrentUserProfile.TotalCoinsEarned += coinsToAdd;
        while (CurrentUserProfile.CurrentXP >= GetXpNeededForLevel(CurrentUserProfile.Level))
        {
            CurrentUserProfile.CurrentXP -= GetXpNeededForLevel(CurrentUserProfile.Level); CurrentUserProfile.Level++;
        }
        await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile));
    }

    private static int GetXpNeededForLevel(int level) => 50 + (level * 5);

    private async Task CheckAchievementsAsync()
    {

        var achievements = await _databaseService.GetAchievementsAsync(_currentUserId);

        var unlockedAny = false; string unlockedTitles = "";

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
        if (CurrentUserProfile.TotalCoinsEarned >= 5000) await Unlock("Atiza como cotiza", "Ganaste 5000 monedas");
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
            await ShowAeroAlert("¡LOGRO DESBLOQUEADO!", $"Has conseguido:\n\n{unlockedTitles}");
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile));
        }
    }

    [RelayCommand]
    private async Task PickImageAsync() { await _soundService.PlayClickAsync(); var photo = await MediaPicker.Default.PickPhotoAsync(); if (photo != null) _pendingImagePath = photo.FullPath; }

    [RelayCommand]
    private async Task AddNewTaskAsync() { await _soundService.PlayClickAsync(); _pendingImagePath = null; NewTaskTitle = ""; NewTaskRewardCoinsText = "10"; NewTaskDate = DateTime.Today; NewTaskTime = DateTime.Now.AddHours(1).TimeOfDay; IsCreatingTask = true; }

    [RelayCommand]
    private async Task SaveTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle) || !int.TryParse(NewTaskRewardCoinsText, out int rewardCoins) || rewardCoins < 0)
        { await _soundService.PlayFailAsync(); await ShowAeroAlert("Error", "Revisa el nombre y las monedas."); return; }

        var newTask = new TaskItem { UserId = _currentUserId, Title = NewTaskTitle.Trim(), DueDateTime = NewTaskDate.Date.Add(NewTaskTime), RewardCoins = rewardCoins, IsCompleted = false, IsFailed = false, ImagePath = _pendingImagePath };
        await _databaseService.SaveTaskAsync(newTask); Tasks.Add(newTask);

        await ScheduleTaskNotifications(newTask); // 🚀 ¡Programamos la notificación al guardar!

        await _soundService.PlayCreatedAsync(); _pendingImagePath = null; IsCreatingTask = false;
    }
    [RelayCommand]
    private async Task CancelTaskAsync() { await _soundService.PlayClickAsync(); IsCreatingTask = false; _pendingImagePath = null; }

    [RelayCommand]
    private async Task AddNewHabitAsync() { await _soundService.PlayClickAsync(); _pendingImagePath = null; NewHabitTitle = ""; NewHabitRewardCoinsText = "5"; NewHabitTime = DateTime.Now.AddHours(1).TimeOfDay; IsCreatingHabit = true; }

    [RelayCommand]
    private async Task SaveHabitAsync()
    {
        if (string.IsNullOrWhiteSpace(NewHabitTitle) || !int.TryParse(NewHabitRewardCoinsText, out int rewardCoins) || rewardCoins < 0)
        { await _soundService.PlayFailAsync(); await ShowAeroAlert("Error", "Revisa el nombre y las monedas."); return; }

        var dayTokens = string.Join(",", WeekDays.Where(d => d.IsSelected).Select(d => d.NumberValue));
        if (string.IsNullOrEmpty(dayTokens)) { await _soundService.PlayFailAsync(); await ShowAeroAlert("Error", "Selecciona al menos un día."); return; }

        var newHabit = new HabitItem { UserId = _currentUserId, Title = NewHabitTitle.Trim(), ScheduledTime = NewHabitTime, ActiveDaysCsv = dayTokens, RewardCoins = rewardCoins, IsPositive = true, ImagePath = _pendingImagePath };
        await _databaseService.SaveHabitAsync(newHabit); Habits.Add(newHabit);

        await ScheduleHabitNotifications(newHabit); // 🚀 ¡Programamos la notificación del hábito!

        await _soundService.PlayCreatedAsync(); _pendingImagePath = null; IsCreatingHabit = false; await CheckAchievementsAsync();
    }
    [RelayCommand]
    private async Task CancelHabitAsync() { await _soundService.PlayClickAsync(); IsCreatingHabit = false; _pendingImagePath = null; }

    [RelayCommand]
    private async Task AddNewRewardAsync()
    {
        await _soundService.PlayClickAsync();
        var maxCustomRewards = 15 + (CurrentUserProfile.Level * 5);
        if (Rewards.Count(r => !r.IsSystemReward) >= maxCustomRewards) { await ShowAeroAlert("Mochila Llena", $"Alcanzaste el tope ({maxCustomRewards})."); return; }
        _pendingImagePath = null; NewRewardTitle = ""; NewRewardCostText = "50"; IsCreatingReward = true;
    }

    [RelayCommand]
    private async Task SaveRewardAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRewardTitle) || !int.TryParse(NewRewardCostText, out int cost) || cost < 0)
        { await _soundService.PlayFailAsync(); await ShowAeroAlert("Error", "Revisa el nombre y coste."); return; }
        var newReward = new RewardItem { UserId = _currentUserId, Title = NewRewardTitle.Trim(), Cost = cost, ImagePath = _pendingImagePath };
        await _databaseService.SaveRewardAsync(newReward); Rewards.Add(newReward); await _soundService.PlayCreatedAsync(); _pendingImagePath = null; IsCreatingReward = false;
    }
    [RelayCommand]
    private async Task CancelRewardAsync() { await _soundService.PlayClickAsync(); IsCreatingReward = false; _pendingImagePath = null; }

    [RelayCommand]
    private async Task DeleteTaskAsync(TaskItem task)
    {
        try
        {
            await _soundService.PlayClickAsync(); if (task is null || !await ShowAeroAlert("Eliminar", "¿Borrar esta tarea?", true)) return;
            await _databaseService.DeleteTaskAsync(task); Tasks.Remove(task);
            CancelTaskNotifications(task.Id); // 🧹 Limpiamos sus notificaciones
            await _soundService.PlayDeletedAsync();
        }
        catch (Exception ex) { await ShowAeroAlert("Error", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteHabitAsync(HabitItem habit)
    {
        try
        {
            await _soundService.PlayClickAsync(); if (habit is null || !await ShowAeroAlert("Eliminar", "¿Borrar este hábito?", true)) return;
            await _databaseService.DeleteHabitAsync(habit); Habits.Remove(habit);
            CancelHabitNotifications(habit.Id); // 🧹 Limpiamos sus notificaciones
            await _soundService.PlayDeletedAsync();
        }
        catch (Exception ex) { await ShowAeroAlert("Error", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteRewardAsync(RewardItem reward)
    {
        try
        {
            await _soundService.PlayClickAsync(); if (reward is null || reward.IsSystemReward || !await ShowAeroAlert("Eliminar", "¿Borrar este capricho?", true)) return;
            await _databaseService.DeleteRewardAsync(reward); Rewards.Remove(reward); await _soundService.PlayDeletedAsync();
        }
        catch (Exception ex) { await ShowAeroAlert("Error", ex.Message); }
    }

    [RelayCommand]
    private async Task CompleteTaskAsync(TaskItem task)
    {
        if (task is null || task.IsCompleted) return;
        CancelTaskNotifications(task.Id); // 🧹 Ya la completaste, que no suene el fracaso

        if (task.DueDateTime <= DateTime.Now)
        {
            task.IsFailed = true; await _databaseService.SaveTaskAsync(task); CurrentUserProfile.Health -= 10;
            if (CurrentUserProfile.Health <= 0) await ApplyGameOverAsync(); else { await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile)); }
            await _soundService.PlayFailAsync(); await ShowAeroAlert("Fallida", $"Llegaste tarde a {task.Title}"); await LoadTasksAsync(); return;
        }
        task.IsCompleted = true; await _databaseService.SaveTaskAsync(task); CurrentUserProfile.TotalTasksCompleted++;
        await AddExperienceAsync(10, task.RewardCoins); await LoadTasksAsync(); await _soundService.PlayTaDaAsync(); await CheckAchievementsAsync();
    }

    [RelayCommand]
    private async Task ExecuteHabitAsync(HabitItem habit)
    {
        if (habit is null) return;
        var today = DateTime.Today; var todayCode = ((int)today.DayOfWeek).ToString(CultureInfo.InvariantCulture);
        var activeDays = (habit.ActiveDaysCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!activeDays.Contains(todayCode)) { await ShowAeroAlert("Aviso", "No toca hoy."); return; }
        if (habit.LastCompletedDate.Date == today) { await ShowAeroAlert("Aviso", "Ya completado hoy."); return; }

        CancelHabitNotifications(habit.Id); // 🧹 Ya la completaste hoy, cancelamos el fracaso

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
            CurrentUserProfile.Coins -= reward.Cost; CurrentUserProfile.TotalRewardsBought++;
            if (reward.HealthRestore > 0) { CurrentUserProfile.Health = Math.Min(CurrentUserProfile.MaxHealth, CurrentUserProfile.Health + reward.HealthRestore); CurrentUserProfile.UsedHealReward = true; }
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile); OnPropertyChanged(nameof(CurrentUserProfile));
            await _soundService.PlayRewardBoughtAsync(); await ShowAeroAlert("¡Premio!", "Disfruta tu recompensa."); await CheckAchievementsAsync(); return;
        }
        await _soundService.PlayFailAsync(); await ShowAeroAlert("Aviso", "No hay monedas suficientes.");
    }

    [ObservableProperty]
    private ObservableCollection<DayBubble> weekDays = new()
    {
        new DayBubble { Name = "L", NumberValue = "1", IsSelected = false }, new DayBubble { Name = "M", NumberValue = "2", IsSelected = false },
        new DayBubble { Name = "X", NumberValue = "3", IsSelected = false }, new DayBubble { Name = "J", NumberValue = "4", IsSelected = false },
        new DayBubble { Name = "V", NumberValue = "5", IsSelected = false }, new DayBubble { Name = "S", NumberValue = "6", IsSelected = false },
        new DayBubble { Name = "D", NumberValue = "0", IsSelected = false }
    };

    [RelayCommand]
    private async Task ToggleDayAsync(DayBubble bubble)
    {
        if (bubble != null)
        {
            bubble.IsSelected = !bubble.IsSelected;
            await _soundService.PlayClickAsync(); // 🔊 ¡Sonido de las burbujas!
        }
    }
}