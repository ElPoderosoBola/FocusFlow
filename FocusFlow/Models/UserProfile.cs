using SQLite;

namespace FocusFlow.Models
{
    public class UserProfile
    {
        [PrimaryKey]
        public int Id { get; set; }

        public int Level { get; set; } = 1;

        public int CurrentXP { get; set; } = 0;
    }
}
