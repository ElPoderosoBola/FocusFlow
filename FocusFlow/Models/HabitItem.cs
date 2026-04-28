using SQLite;

namespace FocusFlow.Models
{
    public class HabitItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Title { get; set; }

        public TimeSpan ScheduledTime { get; set; }

        public string ActiveDaysCsv { get; set; }

        public int RewardCoins { get; set; }

        public DateTime LastCompletedDate { get; set; }

        public DateTime LastPenaltyDate { get; set; }

        public bool IsPositive { get; set; }

        public bool IsNegative { get; set; }

        public string ImagePath { get; set; }
    }
}
