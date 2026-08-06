using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    internal record OutboxEvent(
        long Id,
        string EventType,
        string PayloadJson,
        string Status, // PENDING, PROCESSED, FAILED
        int RetryCount,
        DateTime CreatedAt,
        DateTime? ProcessedAt
    );
    
}
