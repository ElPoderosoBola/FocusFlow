using System;
using SQLite; // ¡LA LIBRERÍA MAGICA DE LA BASE DE DATOS!

namespace FocusFlow.Models
{
    public class TaskItem
    {
        [PrimaryKey, AutoIncrement] // ¡EL DNI DE LA TAREA!
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
        public int EstimatedMinutes { get; set; }
        public DateTime DueDateTime { get; set; }
        public int RewardCoins { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsFailed { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ImagePath { get; set; }
    }
}