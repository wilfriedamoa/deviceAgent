using deviceAgent.DTO;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services.grpc
{
    internal class PrinterGrpcService(ICommandDispatcher commandDispatcher,ILogger<PrinterGrpcService> logger,IPrinterService printerService):PrinterGrpc.PrinterGrpcBase 
    {
        private readonly ICommandDispatcher _commandDispatcher = commandDispatcher;
        private readonly ILogger<PrinterGrpcService> _logger = logger;
        private readonly IPrinterService _printerService = printerService;



        /// <summary>
        /// Point d'entrée gRPC pour l'émission et la personnalisation d'une carte (Magnétique, Contact, Contactless).
        /// </summary>
        public override async Task<CardIssuanceResponse> IssuePersonalizedCard(
            IssueCardRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation(
                "RPC IssuePersonalizedCard reçue [TxId: {TxId}, Client: {FirstName} {LastName}, Mode: {Mode}]",
                request.TransactionId,
                request.CardData?.FirstName,
                request.CardData?.LastName,
                request.CardData?.EncodingMode
            );

            // 1. Validation de la requête entrante
            if (request.CardData == null)
            {
                _logger.LogWarning("Requête gRPC invalide : CardData est null pour TxId {TxId}", request.TransactionId);
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "Les données du titulaire (card_data) sont obligatoires."
                ));
            }

            if (request.TransactionId <= 0)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "Le transaction_id doit être un entier positif valide."
                ));
            }

            try
            {
                // 2. Mapping des Pistes Magnétiques ISO
                MagneticTracksData? magTracks = null;
                if (request.CardData.MagneticTracks != null)
                {
                    magTracks = new MagneticTracksData(
                        Track1: request.CardData.MagneticTracks.Track1,
                        Track2: request.CardData.MagneticTracks.Track2,
                        Track3: request.CardData.MagneticTracks.Track3
                    );
                }

                // 3. Mapping de l'Enumération des Modes d'Encodage (Protobuf -> Flags C#)
                EncodingMode domainEncodingMode = request.CardData.EncodingMode switch
                {
                    EncodingModeDto.EncodingMagnetic => EncodingMode.Magnetic,
                    EncodingModeDto.EncodingSmartContact => EncodingMode.SmartContact,
                    EncodingModeDto.EncodingSmartContactless => EncodingMode.SmartContactless,
                    // Le mode Dual Chip active à la fois le contact ET le contactless
                    EncodingModeDto.EncodingDualChip => EncodingMode.SmartContact | EncodingMode.SmartContactless,
                    _ => EncodingMode.None
                };

                // 4. Mapping du Type de Puce
                ChipType domainChipType = request.ChipType switch
                {
                    ChipTypeDto.ChipIso7816Native => ChipType.Iso7816Native,
                    ChipTypeDto.ChipDesfireEv => ChipType.DesfireEV,
                    _ => ChipType.DesfireEV
                };

                // 5. Instanciation du modèle DTO C# du Domaine
                var domainCardData = new CardHolderData(
                    CardNumber: request.CardData.CardNumber,
                    FirstName: request.CardData.FirstName,
                    LastName: request.CardData.LastName,
                    ExpiryDate: request.CardData.ExpiryDate,
                    PinCode: request.CardData.PinCode,
                    EncodingMode: domainEncodingMode,
                    MagneticTracks: magTracks,
                    ChipDataPayload: request.CardData.ChipDataPayload
                );

                // 6. Exécution du workflow via le Command Dispatcher (Outbox + Traitement matériel)
                var result = await _commandDispatcher.DispatchPersonalizedCardIssuanceAsync(
                    transactionId: request.TransactionId,
                    cardData: domainCardData,
                    chipType: domainChipType,
                    ct: context.CancellationToken
                );

                // 7. Mapping de la réponse Domaine -> Protobuf gRPC
                return new CardIssuanceResponse
                {
                    Success = result.Success,
                    ChipUid = result.ChipUid ?? string.Empty,
                    ErrorCode = result.ErrorCode ?? string.Empty,
                    Message = result.Message ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du traitement gRPC de l'émission pour TxId {TxId}", request.TransactionId);

                return new CardIssuanceResponse
                {
                    Success = false,
                    ChipUid = string.Empty,
                    ErrorCode = "GRPC_SERVICE_EXCEPTION",
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Interroge le statut de santé du composant d'impression.
        /// </summary>
        public override async Task<PrinterStatusResponse> GetPrinterStatus(EmptyRequest request, ServerCallContext context)
        {
            try
            {
                var status = await _printerService.GetStatusAsync(context.CancellationToken);

                return new PrinterStatusResponse
                {
                    Status = status.ToString().ToUpper(),
                    Message = $"Statut imprimante : {status}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération du statut gRPC.");
                return new PrinterStatusResponse
                {
                    Status = "ERROR",
                    Message = ex.Message
                };
            }
        }



    }
}
