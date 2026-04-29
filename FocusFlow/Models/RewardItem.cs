using SQLite;

namespace FocusFlow.Models
{
    public class RewardItem
    {
        [PrimaryKey, AutoIncrement] // <-- ¡Vital para que la base de datos no se líe!
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
        public int Cost { get; set; }
        public string ImagePath { get; set; }
        public bool IsSystemReward { get; set; } // Marca si es la poción fija
        public int HealthRestore { get; set; }  // Cuánta vida cura
    }
}