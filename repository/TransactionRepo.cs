using deviceAgent.data;
using deviceAgent.interfaces;
using deviceAgent.model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.repository
{
    internal class TransactionRepo(IDbContextFactory<AgentDbContext> dbContextFactory) : ITransaction
    {
        private readonly IDbContextFactory<AgentDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Transaction> CreateTransactionAsync(string transactionRef, CancellationToken ct = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var transaction = new Transaction
            {
                TransactionId = transactionRef,
                TransactionStatus = "INITIATED",
                Status = 1,
                
            };
            context.Transactions.Add(transaction);
            await context.SaveChangesAsync(ct);
            return transaction;
        }

        public async Task<Transaction?> GetByRefAsync(string transactionRef, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            return await db.Transactions
                .Include(t => t.CardJobs)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionRef, ct);
        }

        public async Task UpdateStatusAsync(long transactionId, string status, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var tx = await db.Transactions.FindAsync(new object[] { transactionId }, ct);
            if (tx != null)
            {
                tx.TransactionStatus = status;
                tx.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

    }
}
