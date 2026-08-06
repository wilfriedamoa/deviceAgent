using deviceAgent.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    /// <summary>
    /// Contrat du service d'impression pour le contrôle des cartes et le suivi du statut.
    /// </summary>
    internal interface IPrinterService
    {
        /// <summary>
        /// Événement déclenché lors d'un changement de statut de l'imprimante (ex: prêt, bourrage, plus de cartes).
        /// </summary>
        event EventHandler<PrinterEventArgs>? OnPrinterStatusChanged;

        /// <summary>
        /// Exécute une commande d'impression/encodage de carte.
        /// </summary>
        /// <param name="request">Informations du job d'impression (JobId, Payload, Type).</param>
        /// <param name="cancellationToken">Jeton d'annulation.</param>
        /// <returns>Le résultat de l'exécution de l'impression.</returns>
        //Task<PrinteResult> PrintAsync(PrinterJobRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Interroge l'état actuel du périphérique d'impression.
        /// </summary>
        /// <param name="cancellationToken">Jeton d'annulation.</param>
        /// <returns>Le statut matériel actuel.</returns>
        Task<DevicePrinterStatus> GetStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exécute un cycle complet : Insertion -> Encodage Pistes ISO -> Encodage Puce & PIN -> Impression Visuelle -> Distribution.
        /// </summary>
        Task<CardIssuanceResult> IssuePersonalizedCardAsync(
            CardHolderData cardData,
            ChipType chipType,
            CancellationToken ct = default);
    }
}
