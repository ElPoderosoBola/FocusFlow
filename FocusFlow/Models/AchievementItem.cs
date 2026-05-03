using SQLite;

namespace FocusFlow.Models
{
    public class AchievementItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int UserId { get; set; } // 👈 ¡NUEVA ETIQUETA DE DUEÑO!

        public string Title { get; set; }

        public string Description { get; set; }

        public bool IsUnlocked { get; set; } = false;
    }
}