using deviceAgent.DTO;
using deviceAgent.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.interfaces
{
    internal interface IEventQueue
    {
     

        Task EnqueueEventAsync<T>(string eventType, T payload, CancellationToken ct = default);
        Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(int batchSize, CancellationToken ct = default);
        Task MarkEventAsProcessedAsync(long eventId, CancellationToken ct = default);
        Task MarkEventAsFailedAsync(long eventId, string reason, CancellationToken ct = default);
        Task IncrementRetryCountAsync(long eventId, int currentRetryCount, CancellationToken ct = default);
    }
}
