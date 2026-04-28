using SQLite;

namespace FocusFlow.Models
{
    public class UserSession
    {
        [PrimaryKey]
        public int Id { get; set; } = 1;

        public int CurrentUserId { get; set; }

        public DateTime LastAccessDate { get; set; } = DateTime.Today;
    }
}
