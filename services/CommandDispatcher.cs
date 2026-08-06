using deviceAgent.DTO;
using deviceAgent.repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal class CommandDispatcher(ILogger<CommandDispatcher> logger, ICardDispenserService cardDispenserService, EventQueueRepo eventQueueRepo, AuditLogRepo auditLogRepo) : ICommandDispatcher
    {
        private readonly ILogger<CommandDispatcher> _logger = logger;
        private readonly ICardDispenserService _cardDispenserService = cardDispenserService;
        private readonly EventQueueRepo _eventQueueRepo = eventQueueRepo;
        private readonly AuditLogRepo _auditLogRepo = auditLogRepo;

        public async Task<CardIssuanceResult> DispatchPersonalizedCardIssuanceAsync(
         long transactionId,
         CardHolderData cardData,
         ChipType chipType = ChipType.DesfireEV,
         CancellationToken ct = default)
        {
            _logger.LogInformation(
                "Réception de la commande d'émission [TxId: {TxId}] pour {FirstName} {LastName} (Modes: {Mode})",
                transactionId,
                cardData.FirstName,
                cardData.LastName,
                cardData.EncodingMode
            );

            await _auditLogRepo.LogAsync(
                component: "CommandDispatcher",
                action: "DISPATCH_CARD_ISSUANCE",
                statusCode: "RECEIVED",
                details: $"TxId: {transactionId}, Customer: {cardData.FirstName} {cardData.LastName}, Mode: {cardData.EncodingMode}",
                ct
            );

            try
            {
                // 1. Exécution du workflow matériel de distribution via CardDispenserService
                var result = await _cardDispenserService.ProcessCardDispenseAsync(transactionId, cardData, chipType, ct);

                // 2. Préparation du payload de l'événement Outbox pour le CMS
                var eventPayload = new
                {
                    TransactionId = transactionId,
                    CardNumber = cardData.CardNumber,
                    ChipUid = result.ChipUid,
                    Success = result.Success,
                    ErrorCode = result.ErrorCode,
                    Message = result.Message,
                    EncodingModeUsed = cardData.EncodingMode.ToString(),
                    ProcessedAt = DateTime.UtcNow
                };

                string eventType = result.Success ? "CardIssuanceCompleted" : "CardIssuanceFailed";

                // 3. Pattern Outbox : Emprisonnement de l'événement dans la BDD SQLite
                await _eventQueueRepo.EnqueueEventAsync(
                    eventType: eventType,
                    payload: eventPayload,
                    ct: ct
                );

                _logger.LogInformation(
                    "Événement Outbox '{EventType}' empilé avec succès pour TxId {TxId}",
                    eventType,
                    transactionId
                );

                // 4. Log d'audit final
                await _auditLogRepo.LogAsync(
                    component: "CommandDispatcher",
                    action: "DISPATCH_CARD_ISSUANCE",
                    statusCode: result.Success ? "SUCCESS" : "FAILED",
                    details: $"TxId: {transactionId}, Code: {result.ErrorCode}, Message: {result.Message}",
                    ct
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur critique non gérée lors du dispatch de l'émission pour la transaction {TxId}", transactionId);

                var failureResult = new CardIssuanceResult(
                    Success: false,
                    ChipUid: null,
                    ErrorCode: "DISPATCH_CRITICAL_ERROR",
                    Message: ex.Message
                );

                // Enregistrement de l'échec critique dans l'Outbox pour le CMS
                try
                {
                    await _eventQueueRepo.EnqueueEventAsync(
                        eventType: "CardIssuanceFailed",
                        payload: new
                        {
                            TransactionId = transactionId,
                            ErrorCode = "DISPATCH_CRITICAL_ERROR",
                            Error = ex.Message,
                            FailedAt = DateTime.UtcNow
                        },
                        ct: ct
                    );

                    await _auditLogRepo.LogAsync(
                        component: "CommandDispatcher",
                        action: "DISPATCH_CARD_ISSUANCE",
                        statusCode: "CRITICAL_ERROR",
                        details: $"TxId: {transactionId}, Ex: {ex.Message}",
                        ct
                    );
                }
                catch (Exception outboxEx)
                {
                    _logger.LogError(outboxEx, "Impossible d'enregistrer l'événement d'échec dans l'Outbox SQLite.");
                }

                return failureResult;
            }

        }
    }
}
