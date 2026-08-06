using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.model
{
    internal class Transaction : BaseM
    {
        public string TransactionId { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty; 
        
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public string TransactionStatus { get; set; } = "INITIATED"; // INITIATED, PROCESSING, COMPLETED, FAILED, CANCELLED
        public ICollection<CardJob> CardJobs { get; set; } = new List<CardJob>();
    }
}
