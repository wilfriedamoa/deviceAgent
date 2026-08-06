using deviceAgent.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.interfaces
{
    internal interface ITransaction
    {
        Task<Transaction> CreateTransactionAsync(string transactionRef, CancellationToken ct = default);
        Task<Transaction?> GetByRefAsync(string transactionRef, CancellationToken ct = default);
        Task UpdateStatusAsync(long transactionId, string status, CancellationToken ct = default);
    }
}
