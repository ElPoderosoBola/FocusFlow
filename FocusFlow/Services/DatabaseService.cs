using FocusFlow.Models;
using SQLite;

namespace FocusFlow.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _database;

    private async Task InitAsync()
    {
        if (_database is not null) return;

        // 🌟 ¡MAGIA! Pasamos a v4 para que se cree la tabla de logros con la columna UserId
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "focusflow_v4.db3");

        _database = new SQLiteAsyncConnection(databasePath);
        await _database.CreateTableAsync<TaskItem>();
        await _database.CreateTableAsync<HabitItem>();
        await _database.CreateTableAsync<DailyItem>();
        await _database.CreateTableAsync<RewardItem>();
        await _database.CreateTableAsync<AchievementItem>();
        await _database.CreateTableAsync<UserProfile>();
        await _database.CreateTableAsync<User>();
        await _database.CreateTableAsync<UserSession>();

        await EnsureHealthRewardAsync();
        await SeedDataAsync();
    }

    public async Task SeedDataAsync()
    {
        if (_database is null) { await InitAsync(); return; }

        var taskCount = await _database.Table<TaskItem>().CountAsync();
        if (taskCount > 0) return;

        var defaultUser = await _database.Table<User>().FirstOrDefaultAsync();
        if (defaultUser is null) return;

        var sampleTasks = new List<TaskItem>
        {
            new TaskItem { UserId = defaultUser.Id, Title = "Misión de Tutorial", EstimatedMinutes = 20, DueDateTime = DateTime.Now.AddHours(4), RewardCoins = 10, IsCompleted = false, IsFailed = false, ImagePath = string.Empty },
            new TaskItem { UserId = defaultUser.Id, Title = "Repasar C#", EstimatedMinutes = 45, DueDateTime = DateTime.Now.AddHours(8), RewardCoins = 15, IsCompleted = false, IsFailed = false, ImagePath = string.Empty }
        };
        await _database.InsertAllAsync(sampleTasks);
    }

    public async Task<List<TaskItem>> GetTasksAsync(int userId) { await InitAsync(); return await _database!.Table<TaskItem>().Where(t => t.UserId == userId).ToListAsync(); }
    public async Task<TaskItem?> GetTaskByIdAsync(int id, int userId) { await InitAsync(); return await _database!.Table<TaskItem>().Where(t => t.Id == id && t.UserId == userId).FirstOrDefaultAsync(); }
    public async Task<int> SaveTaskAsync(TaskItem item) { await InitAsync(); return item.Id == 0 ? await _database!.InsertAsync(item) : await _database!.UpdateAsync(item); }
    public async Task<int> DeleteTaskAsync(TaskItem item) { await InitAsync(); return await _database!.DeleteAsync(item); }

    public async Task<List<HabitItem>> GetHabitsAsync(int userId) { await InitAsync(); return await _database!.Table<HabitItem>().Where(h => h.UserId == userId).ToListAsync(); }
    public async Task<HabitItem?> GetHabitByIdAsync(int id, int userId) { await InitAsync(); return await _database!.Table<HabitItem>().Where(h => h.Id == id && h.UserId == userId).FirstOrDefaultAsync(); }
    public async Task<int> SaveHabitAsync(HabitItem item) { await InitAsync(); return item.Id == 0 ? await _database!.InsertAsync(item) : await _database!.UpdateAsync(item); }
    public async Task<int> DeleteHabitAsync(HabitItem item) { await InitAsync(); return await _database!.DeleteAsync(item); }

    public async Task<List<DailyItem>> GetDailiesAsync(int userId) { await InitAsync(); return await _database!.Table<DailyItem>().Where(d => d.UserId == userId).ToListAsync(); }
    public async Task<DailyItem?> GetDailyByIdAsync(int id, int userId) { await InitAsync(); return await _database!.Table<DailyItem>().Where(d => d.Id == id && d.UserId == userId).FirstOrDefaultAsync(); }
    public async Task<int> SaveDailyAsync(DailyItem item) { await InitAsync(); return item.Id == 0 ? await _database!.InsertAsync(item) : await _database!.UpdateAsync(item); }
    public async Task<int> DeleteDailyAsync(DailyItem item) { await InitAsync(); return await _database!.DeleteAsync(item); }

    public async Task<List<RewardItem>> GetRewardsAsync(int userId)
    {
        await InitAsync();
        var hasHealthReward = await _database!.Table<RewardItem>().Where(r => r.UserId == userId && r.IsSystemReward).FirstOrDefaultAsync();
        if (hasHealthReward == null)
        {
            var healthReward = new RewardItem { UserId = userId, Title = "Curación de Salud", Cost = 25, ImagePath = "heal.png", IsSystemReward = true, HealthRestore = 10 };
            await _database.InsertAsync(healthReward);
        }
        return await _database!.Table<RewardItem>().Where(r => r.UserId == userId).ToListAsync();
    }
    public async Task<RewardItem?> GetRewardByIdAsync(int id, int userId) { await InitAsync(); return await _database!.Table<RewardItem>().Where(r => r.Id == id && r.UserId == userId).FirstOrDefaultAsync(); }
    public async Task<int> SaveRewardAsync(RewardItem item) { await InitAsync(); return item.Id == 0 ? await _database!.InsertAsync(item) : await _database!.UpdateAsync(item); }
    public async Task<int> DeleteRewardAsync(RewardItem item) { await InitAsync(); return item.IsSystemReward ? 0 : await _database!.DeleteAsync(item); }

    public async Task EnsureHealthRewardAsync()
    {
        await InitAsync();
        var users = await _database!.Table<User>().ToListAsync();
        foreach (var user in users)
        {
            var hasHealthReward = await _database.Table<RewardItem>().Where(r => r.UserId == user.Id && r.IsSystemReward).FirstOrDefaultAsync();
            if (hasHealthReward is not null)
            {
                if (string.IsNullOrEmpty(hasHealthReward.ImagePath)) { hasHealthReward.ImagePath = "heal.png"; await _database.UpdateAsync(hasHealthReward); }
                continue;
            }
            var healthReward = new RewardItem { UserId = user.Id, Title = "Curación de Salud", Cost = 25, ImagePath = "heal.png", IsSystemReward = true, HealthRestore = 10 };
            await _database.InsertAsync(healthReward);
        }
    }

    public async Task<bool> RegisterUserAsync(User user)
    {
        await InitAsync();
        var exists = await _database!.Table<User>().Where(u => u.Username == user.Username).FirstOrDefaultAsync();
        if (exists is not null) return false;

        await _database.InsertAsync(user);

        var profile = new UserProfile { UserId = user.Id, Level = 1, CurrentXP = 0, Coins = 0, Health = 50, MaxHealth = 50, LastLoginDate = DateTime.Today };
        await _database.InsertAsync(profile);

        // 🏆 ¡AQUÍ LE FABRICAMOS SU VITRINA DE LOGROS PERSONAL!
        var personalAchievements = new List<AchievementItem>
        {
            new AchievementItem { UserId = user.Id, Title = "Primeros Pasos", Description = "Completa tu primera misión diaria.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "Aprendiz", Description = "Alcanza el Nivel 2.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "Acaparador", Description = "Consigue 50 monedas.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "Atiza como cotiza", Description = "Gana más de 5000 monedas a lo largo de tu vida.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "El lobo de FocusFlow Street", Description = "Ten 3000 monedas almacenadas en tu cartera.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "Paracetamol y a correr", Description = "Usa la recompensa de curar al menos una vez.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "Y resucitó al tercer día...", Description = "Cae en batalla (Salud a 0) al menos una vez.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "John Multitasking", Description = "Ten al menos 5 hábitos en tu lista.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "Quien bien trabaja, bien descansa", Description = "Compra 5 recompensas en la tienda.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "Ni en Silicon Valley", Description = "Compra 50 recompensas en la tienda.", IsUnlocked = false },
            new AchievementItem { UserId = user.Id, Title = "Autónomo rutinal", Description = "Completa 30 misiones en total.", IsUnlocked = false }
        };
        await _database.InsertAllAsync(personalAchievements);

        return true;
    }

    public async Task<User?> LoginUserAsync(string username, string password) { await InitAsync(); return await _database!.Table<User>().Where(u => u.Username == username && u.Password == password).FirstOrDefaultAsync(); }
    public async Task<UserSession> GetUserSessionAsync()
    {
        await InitAsync();
        var session = await _database!.Table<UserSession>().FirstOrDefaultAsync();
        if (session is not null) return session;
        var newSession = new UserSession { Id = 1, CurrentUserId = 0, LastAccessDate = DateTime.Today };
        await _database.InsertAsync(newSession);
        return newSession;
    }
    public async Task<int> SaveUserSessionAsync(UserSession session) { await InitAsync(); return await _database!.InsertOrReplaceAsync(session); }

    // 🏆 ¡AHORA LOS LOGROS SE PIDEN POR USUARIO! ¡ESTA ERA LA PIEZA QUE FALTABA!
    public async Task<List<AchievementItem>> GetAchievementsAsync(int userId)
    {
        await InitAsync();
        return await _database!.Table<AchievementItem>().Where(a => a.UserId == userId).ToListAsync();
    }
    public async Task<int> UpdateAchievementAsync(AchievementItem item) { await InitAsync(); return await _database!.UpdateAsync(item); }

    public async Task<UserProfile> GetUserProfileAsync(int userId)
    {
        await InitAsync();
        var profile = await _database!.Table<UserProfile>().Where(p => p.UserId == userId).FirstOrDefaultAsync();
        if (profile is not null) return profile;
        var newProfile = new UserProfile { UserId = userId, Level = 1, CurrentXP = 0, Coins = 0, Health = 50, MaxHealth = 50, LastLoginDate = DateTime.Today };
        await _database.InsertAsync(newProfile);
        return newProfile;
    }
    public async Task<int> SaveUserProfileAsync(UserProfile profile) { await InitAsync(); return await _database!.InsertOrReplaceAsync(profile); }
}