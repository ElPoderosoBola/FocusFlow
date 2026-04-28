using System;
using System.Collections.Generic;
using System.Text;

namespace FocusFlow.Models
{
    public class TaskItem
    {
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