using deviceAgent.DTO;
using deviceAgent.repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal class CardDispenseService(
        ILogger<CardDispenseService> logger,
        CardInventoryRepo cardInventoryRepo,
        AuditLogRepo auditLogRepo,
        IPrinterService printerService,
        CardJobRepo cardJobRepo):ICardDispenserService

    {
        private readonly CardInventoryRepo _inventoryRepo=cardInventoryRepo;
        private readonly CardJobRepo _cardJobRepo = cardJobRepo;
        private readonly IPrinterService _printerService=printerService;
        private readonly AuditLogRepo _auditRepo = auditLogRepo;
        private readonly ILogger<CardDispenseService> _logger=logger;


        public async Task<CardIssuanceResult> ProcessCardDispenseAsync(
        long transactionId,
        CardHolderData cardData,
        ChipType chipType = ChipType.Iso7816Native,
        CancellationToken ct = default)
        {
            _logger.LogInformation("Début du traitement de distribution de carte pour la transaction {TxId}", transactionId);

            // 1. Vérification préliminaire du stock dans les cassettes
            var inventory = await _inventoryRepo.GetActiveInventoryForDispenseAsync(ct);
            if (inventory == null || inventory.RemainingQuantity <= 0)
            {
                _logger.LogWarning("Échec de la distribution pour TxId {TxId} : Aucun bac avec du stock disponible.", transactionId);
                await _auditRepo.LogAsync("CardDispenser", "DISPENSE", "FAILED", $"TxId: {transactionId} - Stock de cartes vierges épuisé.", ct);

                return new CardIssuanceResult(
                    Success: false,
                    ChipUid: null,
                    ErrorCode: "NO_STOCK",
                    Message: "Stock de cartes vierges épuisé."
                );
            }

            // 2. Création d'un Job en BDD SQLite avec le statut 'PENDING'
            var job = await _cardJobRepo.CreateJobAsync(transactionId, inventory.FeederCassetteNo, ct);
            _logger.LogInformation("Job de distribution N°{JobId} créé pour la cassette N°{CassetteNo}", job.Id, inventory.FeederCassetteNo);

            try
            {
                // 3. Appel du service d'impression et d'encodage (PrinterSvc -> EvolisCardPrinterDriver)
                var result = await _printerService.IssuePersonalizedCardAsync(cardData, chipType, ct);

                if (!result.Success)
                {
                    _logger.LogWarning("Échec du traitement matériel pour le Job N°{JobId}. Raison: {Message}", job.Id, result.Message);

                    // Mise à jour de la BDD : Enregistrement du rejet et de l'erreur
                    await _cardJobRepo.RejectJobAsync(job.Id, result.Message, ct);
                    await _auditRepo.LogAsync("CardDispenser", "DISPENSE", "REJECTED", $"JobId: {job.Id}, Code: {result.ErrorCode}, Error: {result.Message}", ct);

                    return result;
                }

                // 4. Succès matériel : Mise à jour du Job avec l'UID de la puce détectée
                await _cardJobRepo.UpdateJobProgressAsync(
                    jobId: job.Id,
                    dispenseStatus:"COMPLETED",
                    encodingStatus: "SUCCESS",
                    printingStatus: "SUCCESS",
                    chipUid: result.ChipUid,
                    ct: ct
                );

                await _auditRepo.LogAsync("CardDispenser", "DISPENSE", "SUCCESS", $"JobId: {job.Id}, TxId: {transactionId}, ChipUid: {result.ChipUid}", ct);
                _logger.LogInformation("Distribution réussie pour le Job N°{JobId} [ChipUid: {ChipUid}]", job.Id, result.ChipUid);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur fatale imprévue pendant le traitement de distribution du Job N°{JobId}", job.Id);

                await _cardJobRepo.RejectJobAsync(job.Id, ex.Message, ct);
                await _auditRepo.LogAsync("CardDispenser", "DISPENSE", "CRITICAL_ERROR", $"JobId: {job.Id}, Ex: {ex.Message}", ct);

                return new CardIssuanceResult(
                    Success: false,
                    ChipUid: null,
                    ErrorCode: "DISPENSE_EXCEPTION",
                    Message: ex.Message
                );
            }
        }

    }
}
