using deviceAgent.drivers;
using deviceAgent.DTO;
using deviceAgent.interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal class PrinterService(ILogger<PrinterService> logger,ICardInventory cardInventory,EvolisCardPrinterDriver cardPrinterDriver) : IPrinterService
    {
        private readonly ILogger<PrinterService> _logger = logger;
        private readonly ICardInventory _cardInventory = cardInventory;
        private readonly EvolisCardPrinterDriver _cardPrinterDriver = cardPrinterDriver;
        public event EventHandler<PrinterEventArgs>? OnPrinterStatusChanged;


        /// <summary>
        /// Traite l'émission personnalisée de la carte (Encodage Pistes, Encodage Puce, PIN, Visuel Titulaire).
        /// </summary>
        [SupportedOSPlatform("windows6.1")]
        public async Task<CardIssuanceResult> IssuePersonalizedCardAsync(
        CardHolderData cardData,
        ChipType chipType = ChipType.DesfireEV,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Demande d'émission de carte reçue pour : {FirstName} {LastName} [Modes: {Mode}]", 
            cardData.FirstName, cardData.LastName, cardData.EncodingMode);

        // 1. Vérification de la disponibilité du stock de cartes vierges en BDD SQLite
        var activeInventory = await _cardInventory.GetActiveInventoryForDispenseAsync(ct);
        if (activeInventory == null || activeInventory.RemainingQuantity <= 0)
        {
            _logger.LogWarning("Échec de l'émission : Aucun bac de cartes disponible ou stock épuisé.");
            
            RaiseStatusChanged("NO_JOB", DevicePrinterStatus.OutOfPaper, "Stock de cartes vierges épuisé.");
            return new CardIssuanceResult(
                Success: false, 
                ChipUid: null, 
                ErrorCode: "NO_STOCK", 
                Message: "Tous les bacs de cartes de la borne sont vides."
            );
        }

        RaiseStatusChanged("ISSUANCE_JOB", DevicePrinterStatus.Busy, "Cycle d'émission de la carte en cours...");

        // 2. Exécution du cycle matériel complet (Feeder -> Encodage -> Impression -> Eject)
        var result = await _cardPrinterDriver.IssuePersonalizedCardAsync(cardData, chipType, ct);

        // 3. Traitement du résultat matériel
        if (!result.Success)
        {
            _logger.LogError("Échec du cycle matériel sur l'Evolis [Code: {Code}] : {Message}", 
                result.ErrorCode, result.Message);

            // Détermination du statut matériel à notifier selon le code d'erreur
            var errorStatus = result.ErrorCode.Contains("JAM") 
                ? DevicePrinterStatus.Jammed 
                : DevicePrinterStatus.Error;

            RaiseStatusChanged("ISSUANCE_JOB", errorStatus, result.Message);
            return result;
        }

        // 4. Décrémentation du stock en BDD SQLite uniquement en cas de succès de la carte
        try
        {
            await _cardInventory.DecrementStockAsync(activeInventory.FeederCassetteNo, ct);
            _logger.LogInformation("Stock de la cassette N°{CassetteNo} décrémenté avec succès en BDD.", 
                activeInventory.FeederCassetteNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour du stock de cartes en BDD SQLite.");
            // L'émission matérielle a réussi, donc on ne fait qu'insister sur le log de l'erreur d'inventaire
        }

        RaiseStatusChanged("ISSUANCE_JOB", DevicePrinterStatus.Ready, "Carte émise et distribuée avec succès.");
        
        return result;
    }


        /// <summary>
        /// Interroge l'état de l'imprimante Evolis et remonte le statut matériel global.
        /// </summary>
        public Task<DevicePrinterStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            if (!_cardPrinterDriver.IsConnected)
            {
                return Task.FromResult(DevicePrinterStatus.Offline);
            }

            // Interrogation des capteurs matériels
            var statusResult = _cardPrinterDriver.GetPrinterHardwareStatus();

            if (!String.IsNullOrEmpty(statusResult))
            {
                return Task.FromResult(statusResult switch
                {
                    "ERROR_FEEDER_EMPTY" => DevicePrinterStatus.OutOfPaper,
                    "ERROR_RIBBON_NO" => DevicePrinterStatus.Offline,
                    "ERROR_COVER_OPEN" => DevicePrinterStatus.Jammed,
                    _ => DevicePrinterStatus.Error
                });
            }

            return Task.FromResult(DevicePrinterStatus.Ready);
        }

        private void RaiseStatusChanged(string jobId, DevicePrinterStatus status, string message)
        {
            OnPrinterStatusChanged?.Invoke(this, new PrinterEventArgs
            {
                JobId = jobId,
                Status = status,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
