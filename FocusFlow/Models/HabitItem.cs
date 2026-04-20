using SQLite;

namespace FocusFlow.Models
{
    public class HabitItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Title { get; set; }

        public bool IsPositive { get; set; }

        public bool IsNegative { get; set; }
    }
}
