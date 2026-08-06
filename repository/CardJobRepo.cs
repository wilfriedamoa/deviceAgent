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
    internal class CardJobRepo(IDbContextFactory<AgentDbContext> dbContextFactory) : ICardJob
    {
        private readonly IDbContextFactory<AgentDbContext> _dbContextFactory = dbContextFactory;

        public async Task<CardJob> CreateJobAsync(long transactionId, int feederUsed, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var job = new CardJob
            {
                TransactionId = transactionId,
                FeederUsed = feederUsed,
                EncodingStatus = "PENDING",
                PrintingStatus = "PENDING",
                DispenseStatus = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            db.CardJobs.Add(job);
            await db.SaveChangesAsync(ct);
            return job;
        }

        public async Task UpdateJobProgressAsync(long jobId, string encodingStatus, string printingStatus, string dispenseStatus, string? serialNum = null, string? chipUid = null, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var job = await db.CardJobs.FindAsync(new object[] { jobId }, ct);
            if (job != null)
            {
                job.EncodingStatus = encodingStatus;
                job.PrintingStatus = printingStatus;
                job.DispenseStatus = dispenseStatus;
                if (serialNum != null) job.CardSerialNumber = serialNum;
                if (chipUid != null) job.ChipUid = chipUid;
                if (dispenseStatus == "COLLECTED" || dispenseStatus == "REJECTED") job.CompletedAt = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);
            }
        }

        public async Task RejectJobAsync(long jobId, string reason, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var job = await db.CardJobs.FindAsync(new object[] { jobId }, ct);
            if (job != null)
            {
                job.IsRejected = true;
                job.RejectReason = reason;
                job.DispenseStatus = "REJECTED";
                job.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        public async Task<List<CardJob>> GetJobsByTransactionIdAsync(long transactionId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            return await db.CardJobs.Where(j => j.TransactionId == transactionId).ToListAsync(ct);
        }

    }
}
