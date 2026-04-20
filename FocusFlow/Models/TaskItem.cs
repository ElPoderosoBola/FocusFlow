using System;
using System.Collections.Generic;
using System.Text;

namespace FocusFlow.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int EstimatedMinutes { get; set; }
        public bool IsCompleted { get; set; }
    }
}