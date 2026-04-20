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

    // Lista observable para que la UI se actualice cuando cambien las tareas.
    [ObservableProperty]
    private ObservableCollection<TaskItem> tasks = new();

    // Lista observable de hábitos.
    [ObservableProperty]
    private ObservableCollection<HabitItem> habits = new();

    // Lista observable de dailies.
    [ObservableProperty]
    private ObservableCollection<DailyItem> dailies = new();

    // Puntos de experiencia actuales del usuario.
    [ObservableProperty]
    private int currentXP = 0;

    // Nivel actual del usuario.
    [ObservableProperty]
    private int currentLevel = 1;

    public MainViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        // Carga inicial de datos cuando el ViewModel está listo para usarse.
        await LoadTasksAsync();
        await LoadHabitsAsync();
        await LoadDailiesAsync();
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

    private void AddExperience(int amount)
    {
        // Suma experiencia y sube de nivel por cada tramo de 100 XP.
        CurrentXP += amount;

        while (CurrentXP >= 100)
        {
            CurrentLevel++;
            CurrentXP -= 100;
        }
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
        AddExperience(50);

        // Recarga las tareas para reflejar el estado actualizado.
        await LoadTasksAsync();
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
            AddExperience(10);
        }

        // Si es hábito negativo, resta XP sin bajar de 0.
        if (habit.IsNegative)
        {
            CurrentXP = Math.Max(0, CurrentXP - 10);
        }

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
        AddExperience(20);

        // Guarda cambios en base de datos.
        await _databaseService.SaveDailyAsync(daily);

        // Recarga dailies para reflejar el estado actualizado.
        await LoadDailiesAsync();
    }
}
