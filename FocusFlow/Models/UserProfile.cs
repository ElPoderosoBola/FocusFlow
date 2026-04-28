using SQLite;

namespace FocusFlow.Models
{
    public class UserProfile
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int UserId { get; set; }

        public int Level { get; set; } = 1;

        public int CurrentXP { get; set; } = 0;

        public int Coins { get; set; } = 0;

        public int Health { get; set; } = 50;

        public int MaxHealth { get; set; } = 50;

        public DateTime LastLoginDate { get; set; } = DateTime.Today;
    }
}
