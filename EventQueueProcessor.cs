using deviceAgent.data;
using deviceAgent.DTO;
using deviceAgent.model;
using deviceAgent.repository;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent
{
    internal class EventQueueProcessor(ILogger<EventQueueProcessor> logger,IServiceScopeFactory serviceScopeFactory,EventQueueRepo eventQueue,HttpClient httpClient) : BackgroundService
    {
        private readonly ILogger<EventQueueProcessor> _logger =logger;
        private readonly IServiceScopeFactory _serviceScopeFactory=serviceScopeFactory;
        private readonly EventQueueRepo _eventQueueRepo=eventQueue;
        private readonly HttpClient _httpClient=httpClient;

        // Configuration du polling et des réessais
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(3);
        private const int MaxBatchSize = 10;
        private const int MaxRetryCount = 5;


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 EventQueueProcessor (Outbox Worker) démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingEventsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Arrêt normal du service
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur inattendue dans la boucle d'exécution d'EventQueueProcessor.");
                }

                // Pause entre deux cycles de scrutation (polling)
                await Task.Delay(PollingInterval, stoppingToken);
            }

            _logger.LogInformation("🛑 EventQueueProcessor arrêté.");
        }



        /// <summary>
        /// Récupère un lot d'événements en attente et tente de les publier sur le CMS.
        /// </summary>
        private async Task ProcessPendingEventsAsync(CancellationToken ct)
        {
            // 1. Récupération des événements à l'état "PENDING"
            var pendingEvents = await _eventQueueRepo.GetPendingEventsAsync(MaxBatchSize, ct);

            if (pendingEvents == null || pendingEvents.Count == 0)
            {
                return; // Aucun événement à traiter
            }

            _logger.LogDebug("{Count} événement(s) Outbox en attente de transmission au CMS.", pendingEvents.Count);

            foreach (var evt in pendingEvents)
            {
                if (ct.IsCancellationRequested) break;

                bool success = await DispatchEventToCmsAsync(evt, ct);

                if (success)
                {
                    // 2a. Succès : Marquer l'événement comme publié
                    await _eventQueueRepo.MarkEventAsProcessedAsync(evt.Id, ct);
                    _logger.LogInformation("Événement Outbox N°{EventId} ({EventType}) transmis avec succès au CMS.", evt.Id, evt.EventType);
                }
                else
                {
                    // 2b. Échec : Incrémenter le compteur d'essais
                    int updatedRetryCount = evt.RetryCount + 1;

                    if (updatedRetryCount >= MaxRetryCount)
                    {
                        _logger.LogError("Événement Outbox N°{EventId} abandonné après {MaxRetry} échecs consécutifs.", evt.Id, MaxRetryCount);
                        await _eventQueueRepo.MarkEventAsFailedAsync(evt.Id, $"Dépassé le nombre max d'essais ({MaxRetryCount})", ct);
                    }
                    else
                    {
                        _logger.LogWarning("Échec d'envoi de l'événement N°{EventId}. Nouvelle tentative ({RetryCount}/{MaxCount})...",
                            evt.Id, updatedRetryCount, MaxRetryCount);

                        await _eventQueueRepo.IncrementRetryCountAsync(evt.Id, updatedRetryCount, ct);
                    }
                }
            }
        }

        /// <summary>
        /// Envoie le payload JSON de l'événement vers l'API Endpoint du CMS central via HTTP Post.
        /// </summary>
        private async Task<bool> DispatchEventToCmsAsync(OutboxEvent evt, CancellationToken ct)
        {
            try
            {
                // Endpoint Webhook / Event du CMS central
                string requestUrl = "/api/v1/kiosk/events";

                using var requestContent = JsonContent.Create(new
                {
                    eventId = evt.Id,
                    eventType = evt.EventType,
                    payload = evt.PayloadJson,
                    createdAt = evt.CreatedAt
                });

                _logger.LogInformation("Envoi HTTP POST event [{EventType}] pour EventId {EventId}...", evt.EventType, evt.Id);

                var response = await _httpClient.PostAsync(requestUrl, requestContent, ct);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                _logger.LogWarning("Le CMS a répondu avec le code HTTP {StatusCode} pour l'événement N°{EventId}",
                    response.StatusCode, evt.Id);

                return false;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogWarning("Impossible de contacter le CMS (Réseau/Offline) : {Message}", httpEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la transmission de l'événement N°{EventId}", evt.Id);
                return false;
            }
        }

    
       

    }
}
