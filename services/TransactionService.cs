using deviceAgent.model;
using deviceAgent.repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal class TransactionService(ILogger<TransactionService> logger,TransactionRepo transactionRepo,EventQueueRepo eventQueueRepo,AuditLogRepo auditLogRepo): ITransactionService
    {
        private readonly ILogger<TransactionService> _logger=logger;
        private readonly TransactionRepo _transactionRepo=transactionRepo;
        private readonly EventQueueRepo _eventQueueRepo=eventQueueRepo;
        private readonly AuditLogRepo _auditLogRepo=auditLogRepo;

        public async Task<Transaction> StartTransactionAsync(string transactionRef, CancellationToken ct = default)
        {
            _logger.LogInformation("Démarrage de la transaction : {Ref}", transactionRef);
            var tx = await _transactionRepo.CreateTransactionAsync(transactionRef, ct);

            await _auditLogRepo.LogAsync("TransactionService", "START", "SUCCESS", $"TxRef: {transactionRef}", ct);
            await _eventQueueRepo.EnqueueEventAsync("TransactionStarted", new { TransactionId = tx.Id, tx.CardType }, ct);

            return tx;
        }

        public async Task CompleteTransactionAsync(long transactionId, bool success, string? errorMessage = null, CancellationToken ct = default)
        {
            string status = success ? "COMPLETED" : "FAILED";
            await _transactionRepo.UpdateStatusAsync(transactionId, status, ct);

            await _auditLogRepo.LogAsync("TransactionService", "COMPLETE", status, errorMessage, ct);
            await _eventQueueRepo.EnqueueEventAsync("TransactionEnded", new { TransactionId = transactionId, Status = status, Error = errorMessage }, ct);
        }
    }
}
