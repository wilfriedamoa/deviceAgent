using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.model
{
    internal class CardJob:BaseM
    {
        public long TransactionId { get; set; }
        public string? CardSerialNumber { get; set; }
        public string? ChipUid { get; set; }
        public int FeederUsed { get; set; }

        public string EncodingStatus { get; set; } = "PENDING"; // PENDING, SUCCESS, FAILED
        public string PrintingStatus { get; set; } = "PENDING"; // PENDING, SUCCESS, FAILED, SKIPPED
        public string DispenseStatus { get; set; } = "PENDING"; // PENDING, PRESENTED, COLLECTED, REJECTED

        public bool IsRejected { get; set; } = false;
        public string? RejectReason { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Propriétés de navigation
        public Transaction Transaction { get; set; } = null!;
        public CardInventory FeederInventory { get; set; } = null!;
    }
}
