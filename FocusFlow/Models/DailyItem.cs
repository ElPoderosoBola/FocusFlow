using SQLite;

namespace FocusFlow.Models
{
    public class DailyItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Title { get; set; }

        public bool IsCompletedToday { get; set; }

        public DateTime LastCompletedDate { get; set; }

        public int Streak { get; set; }
    }
}
