using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.model
{
    internal class EventQueue : BaseM
    {
        public string EventType { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public bool IsProcessed { get; set; } = false;
        public int RetryCount { get; set; } = 0;
        public string? LastError { get; set; }
       
        public DateTime? ProcessedAt { get; set; }
    }
}
