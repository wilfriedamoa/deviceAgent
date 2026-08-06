using deviceAgent.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal interface IHealthService
    {
        /// <summary>
        /// Exécute un diagnostic complet des périphériques matériels, de la base SQLite et des ressources système (RAM, Disque).
        /// </summary>
        /// <param name="cancellationToken">Jeton d'annulation.</param>
        /// <returns>Un rapport de santé structuré de la borne.</returns>
        Task<SystemHealthReport> PerformHealthCheckAsync(CancellationToken cancellationToken = default);
    }
}
