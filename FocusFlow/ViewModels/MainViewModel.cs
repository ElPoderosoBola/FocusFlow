using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusFlow.Models;
using FocusFlow.Services;

namespace FocusFlow.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private bool _isInitialized;
    private IDispatcherTimer _timer;

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
        await LoadRewardsAsync();
        _isInitialized = true;
    }

    public async Task LoadTasksAsync()
    {
        // Lee todas las tareas guardadas en SQLite.
        var taskList = await _databaseService.GetTasksAsync();

        // Reemplaza la colección para refrescar la vista.
        Tasks = new ObservableCollection<TaskItem>(taskList);
    }

    public async Task LoadHabitsAsync()
    {
        // Lee todos los hábitos guardados en SQLite.
        var habitList = await _databaseService.GetHabitsAsync();

        // Reemplaza la colección para refrescar la vista.
        Habits = new ObservableCollection<HabitItem>(habitList);
    }

    public async Task LoadDailiesAsync()
    {
        // Lee todos los dailies guardados en SQLite.
        var dailyList = await _databaseService.GetDailiesAsync();

        // Reemplaza la colección para refrescar la vista.
        Dailies = new ObservableCollection<DailyItem>(dailyList);
    }

    public async Task LoadRewardsAsync()
    {
        // Lee todas las recompensas guardadas en SQLite.
        var rewardList = await _databaseService.GetRewardsAsync();

        // Reemplaza la colección para refrescar la vista.
        Rewards = new ObservableCollection<RewardItem>(rewardList);
    }

    private async Task AddExperienceAsync(int amount, int coinsToAdd = 0)
    {
        // Suma experiencia y monedas, y sube de nivel por cada tramo de 100 XP.
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

    [RelayCommand]
    private async Task AddNewHabitAsync()
    {
        // Abre un cuadro nativo para pedir el título del nuevo hábito.
        var title = await Application.Current.MainPage.DisplayPromptAsync(
            "Nuevo Hábito",
            "¿Qué hábito quieres registrar?");

        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var newHabit = new HabitItem
        {
            Title = title.Trim(),
            IsPositive = true,
            IsNegative = false
        };

        // Guarda el nuevo hábito en SQLite.
        await _databaseService.SaveHabitAsync(newHabit);

        // Lo añade a la colección para refrescar la UI al instante.
        Habits.Add(newHabit);
    }

    [RelayCommand]
    private async Task AddNewDailyAsync()
    {
        // Abre un cuadro nativo para pedir el título del nuevo daily.
        var title = await Application.Current.MainPage.DisplayPromptAsync(
            "Nuevo Daily",
            "¿Qué daily quieres registrar?");

        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var newDaily = new DailyItem
        {
            Title = title.Trim(),
            IsCompletedToday = false,
            LastCompletedDate = DateTime.MinValue,
            Streak = 0
        };

        // Guarda el nuevo daily en SQLite.
        await _databaseService.SaveDailyAsync(newDaily);

        // Lo añade a la colección para refrescar la UI al instante.
        Dailies.Add(newDaily);
    }

    [RelayCommand]
    private async Task AddNewTaskAsync()
    {
        // Abre un cuadro nativo para pedir el título de la nueva tarea.
        var title = await Application.Current.MainPage.DisplayPromptAsync(
            "Nueva Tarea",
            "¿Qué tarea quieres registrar?");

        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var newTask = new TaskItem
        {
            Title = title.Trim(),
            EstimatedMinutes = 25,
            IsCompleted = false
        };

        // Guarda la nueva tarea en SQLite.
        await _databaseService.SaveTaskAsync(newTask);

        // La añade a la colección para refrescar la UI al instante.
        Tasks.Add(newTask);
    }

    [RelayCommand]
    private async Task AddNewRewardAsync()
    {
        // Pide el título de la nueva recompensa.
        var title = await Application.Current.MainPage.DisplayPromptAsync(
            "Nueva Recompensa",
            "¿Qué recompensa quieres añadir?");

        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        // Pide el coste de la recompensa en monedas.
        var costText = await Application.Current.MainPage.DisplayPromptAsync(
            "Coste de Recompensa",
            "¿Cuántas monedas cuesta?");

        if (string.IsNullOrWhiteSpace(costText) || !int.TryParse(costText, out var cost) || cost < 0)
        {
            return;
        }

        var newReward = new RewardItem
        {
            Title = title.Trim(),
            Cost = cost
        };

        // Guarda la recompensa y la añade a la lista visible.
        await _databaseService.SaveRewardAsync(newReward);
        Rewards.Add(newReward);
    }

    [RelayCommand]
    private async Task CompleteTaskAsync(TaskItem task)
    {
        if (task is null || task.IsCompleted)
        {
            return;
        }

        // Marca la tarea como completada y la guarda en base de datos.
        task.IsCompleted = true;
        await _databaseService.SaveTaskAsync(task);

        // Suma experiencia por completar una tarea.
        await AddExperienceAsync(50, 10);

        // Recarga las tareas para reflejar el estado actualizado.
        await LoadTasksAsync();
    }

    [RelayCommand]
    private Task StartTimerAsync()
    {
        if (IsTimerRunning)
        {
            return Task.CompletedTask;
        }

        // Arranca el temporizador de concentración.
        IsTimerRunning = true;
        _timer.Start();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task StopTimerAsync()
    {
        // Pausa el temporizador y pregunta si el usuario se rinde.
        _timer.Stop();

        var surrender = await Application.Current.MainPage.DisplayAlert(
            "Detener temporizador",
            "¿Te rindes en esta misión?",
            "Sí",
            "No");

        if (surrender)
        {
            TimeRemainingSeconds = 1500;
            TimerDisplay = "25:00";
            IsTimerRunning = false;
            return;
        }

        // Si no se rinde, reanuda el temporizador.
        IsTimerRunning = true;
        _timer.Start();
    }

    [RelayCommand]
    private async Task ExecuteHabitAsync(HabitItem habit)
    {
        if (habit is null)
        {
            return;
        }

        // Si es hábito positivo, suma XP y revisa subida de nivel.
        if (habit.IsPositive)
        {
            await AddExperienceAsync(10, 10);
        }

        // Si es hábito negativo, resta salud.
        if (habit.IsNegative)
        {
            CurrentUserProfile.Health -= 10;

            // Si no queda salud, aplica penalización tipo Game Over.
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

        // Guarda el perfil tras los cambios de experiencia.
        await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
        OnPropertyChanged(nameof(CurrentUserProfile));

        // Guarda el hábito en base de datos.
        await _databaseService.SaveHabitAsync(habit);
    }

    [RelayCommand]
    private async Task CompleteDailyAsync(DailyItem daily)
    {
        if (daily is null)
        {
            return;
        }

        // Marca el daily como completado hoy y actualiza la fecha.
        daily.IsCompletedToday = true;
        daily.LastCompletedDate = DateTime.Today;

        // Suma XP por completar el daily y revisa subida de nivel.
        await AddExperienceAsync(20, 10);

        // Guarda cambios en base de datos.
        await _databaseService.SaveDailyAsync(daily);

        // Recarga dailies para reflejar el estado actualizado.
        await LoadDailiesAsync();
    }

    [RelayCommand]
    private async Task BuyRewardAsync(RewardItem reward)
    {
        if (reward is null)
        {
            return;
        }

        if (CurrentUserProfile.Coins >= reward.Cost)
        {
            CurrentUserProfile.Coins -= reward.Cost;
            await _databaseService.SaveUserProfileAsync(CurrentUserProfile);
            OnPropertyChanged(nameof(CurrentUserProfile));

            await Application.Current.MainPage.DisplayAlert(
                "Recompensas",
                "¡Premio comprado! Disfruta de tu recompensa",
                "OK");
            return;
        }

        await Application.Current.MainPage.DisplayAlert(
            "Recompensas",
            "No tienes suficientes monedas",
            "OK");
    }
}
