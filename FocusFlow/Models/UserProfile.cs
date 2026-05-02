using SQLite;

namespace FocusFlow.Models
{
    public class UserProfile
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int UserId { get; set; }
        public int Level { get; set; }
        public int CurrentXP { get; set; }
        public int Coins { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public DateTime LastLoginDate { get; set; }

        // 📊 NUESTRO DIARIO DE ESTADÍSTICAS SECRETAS
        public int TotalCoinsEarned { get; set; }
        public int TotalRewardsBought { get; set; }
        public int TotalTasksCompleted { get; set; }
        public int TimesDied { get; set; }
        public bool UsedHealReward { get; set; }
    }
}