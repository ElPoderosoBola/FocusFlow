using SQLite;

namespace FocusFlow.Models
{
    public class RewardItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Title { get; set; }

        public int Cost { get; set; }

        public string ImagePath { get; set; }
    }
}
