using deviceAgent.data;
using deviceAgent.DTO;
using deviceAgent.interfaces;
using deviceAgent.model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace deviceAgent.repository
{
    internal class EventQueueRepo(IDbContextFactory<AgentDbContext> dbContextFactory,ILogger<EventQueueProcessor> logger) : IEventQueue
    {
        private  readonly IDbContextFactory<AgentDbContext> _dbContextFactory = dbContextFactory;
        private readonly ILogger<EventQueueProcessor> _logger=logger;
        public async Task EnqueueEventAsync<T>(string eventType, T payload, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var eventEntity = new EventQueue
            {
                EventType = eventType,
                PayloadJson = JsonSerializer.Serialize(payload),
                IsProcessed = false,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            db.EventQueues.Add(eventEntity);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("Événement Outbox '{EventType}' inséré en BDD (ID: {EventId}).", eventType, eventEntity.Id);
        }
        /// <summary>
        /// Récupère un lot d'événements à traiter (Statut PENDING).
        /// </summary>
        public async Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(int batchSize, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var records = await db.EventQueues
                .AsNoTracking()
                .Where(e => !e.IsProcessed)
                .OrderBy(e => e.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            return records.Select(r => new OutboxEvent(
                Id: r.Id,
                EventType: r.EventType,
                PayloadJson: r.PayloadJson,
                Status: "PENDING",
                RetryCount: r.RetryCount,
                CreatedAt: r.CreatedAt,
                ProcessedAt: r.ProcessedAt
            )).ToList();
        }
        /// <summary>
        /// Marque l'événement comme transmis avec succès au CMS (Statut PROCESSED).
        /// </summary>
        public async Task MarkEventAsProcessedAsync(long eventId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            await db.EventQueues
                .Where(e => e.Id == eventId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.IsProcessed, true)
                    .SetProperty(e => e.ProcessedAt, DateTime.UtcNow), ct);
        }

        /// <summary>
        /// Marque un événement en échec définitif après l'atteinte du nombre max de tentatives.
        /// </summary>
        public async Task MarkEventAsFailedAsync(long eventId, string reason, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            await db.EventQueues
                .Where(e => e.Id == eventId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.IsProcessed, false)
                    .SetProperty(e => e.ProcessedAt, DateTime.UtcNow), ct);

            _logger.LogWarning("Événement N°{EventId} marqué comme FAILED. Raison: {Reason}", eventId, reason);
        }

        /// <summary>
        /// Incrémente le compteur de tentatives d'envoi.
        /// </summary>
        public async Task IncrementRetryCountAsync(long eventId, int currentRetryCount, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            await db.EventQueues
                .Where(e => e.Id == eventId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.RetryCount, currentRetryCount), ct);
        }
    }
}
