using FocusFlow.Models;
using SQLite;

namespace FocusFlow.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _database;

    private async Task InitAsync()
    {
        if (_database is not null)
        {
            return;
        }

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "focusflow.db3");
        _database = new SQLiteAsyncConnection(databasePath);
        await _database.CreateTableAsync<TaskItem>();
        await _database.CreateTableAsync<HabitItem>();
        await _database.CreateTableAsync<DailyItem>();

        // Inserta datos de ejemplo solo si no hay tareas guardadas.
        await SeedDataAsync();
    }

    public async Task SeedDataAsync()
    {
        // Si la base de datos aún no está lista, delega en InitAsync.
        // InitAsync ya vuelve a llamar a este método cuando toca.
        if (_database is null)
        {
            await InitAsync();
            return;
        }

        var taskCount = await _database.Table<TaskItem>().CountAsync();

        if (taskCount > 0)
        {
            return;
        }

        var sampleTasks = new List<TaskItem>
        {
            new TaskItem { Title = "Misión de Tutorial", EstimatedMinutes = 20, IsCompleted = false },
            new TaskItem { Title = "Repasar C#", EstimatedMinutes = 45, IsCompleted = false },
            new TaskItem { Title = "Configurar UI Metro", EstimatedMinutes = 60, IsCompleted = false }
        };

        await _database.InsertAllAsync(sampleTasks);
    }

    public async Task<List<TaskItem>> GetTasksAsync()
    {
        await InitAsync();
        return await _database!.Table<TaskItem>().ToListAsync();
    }

    public async Task<TaskItem?> GetTaskByIdAsync(int id)
    {
        await InitAsync();
        return await _database!.Table<TaskItem>()
            .Where(t => t.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveTaskAsync(TaskItem item)
    {
        await InitAsync();

        if (item.Id == 0)
        {
            return await _database!.InsertAsync(item);
        }

        return await _database!.UpdateAsync(item);
    }

    public async Task<int> DeleteTaskAsync(TaskItem item)
    {
        await InitAsync();
        return await _database!.DeleteAsync(item);
    }

    // CRUD de hábitos.
    public async Task<List<HabitItem>> GetHabitsAsync()
    {
        await InitAsync();
        return await _database!.Table<HabitItem>().ToListAsync();
    }

    public async Task<HabitItem?> GetHabitByIdAsync(int id)
    {
        await InitAsync();
        return await _database!.Table<HabitItem>()
            .Where(h => h.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveHabitAsync(HabitItem item)
    {
        await InitAsync();

        if (item.Id == 0)
        {
            return await _database!.InsertAsync(item);
        }

        return await _database!.UpdateAsync(item);
    }

    public async Task<int> DeleteHabitAsync(HabitItem item)
    {
        await InitAsync();
        return await _database!.DeleteAsync(item);
    }

    // CRUD de dailies.
    public async Task<List<DailyItem>> GetDailiesAsync()
    {
        await InitAsync();
        return await _database!.Table<DailyItem>().ToListAsync();
    }

    public async Task<DailyItem?> GetDailyByIdAsync(int id)
    {
        await InitAsync();
        return await _database!.Table<DailyItem>()
            .Where(d => d.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveDailyAsync(DailyItem item)
    {
        await InitAsync();

        if (item.Id == 0)
        {
            return await _database!.InsertAsync(item);
        }

        return await _database!.UpdateAsync(item);
    }

    public async Task<int> DeleteDailyAsync(DailyItem item)
    {
        await InitAsync();
        return await _database!.DeleteAsync(item);
    }
}
