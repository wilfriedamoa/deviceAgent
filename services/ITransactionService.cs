using deviceAgent.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal interface ITransactionService
    {
        Task<Transaction> StartTransactionAsync(string transactionRef, CancellationToken ct = default);
        Task CompleteTransactionAsync(long transactionId, bool success, string? errorMessage = null, CancellationToken ct = default);
    }
}
