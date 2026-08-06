using deviceAgent.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.interfaces
{
    internal interface ICardJob
    {
        Task<CardJob> CreateJobAsync(long transactionId, int feederUsed, CancellationToken ct = default);
        Task UpdateJobProgressAsync(long jobId, string encodingStatus, string printingStatus, string dispenseStatus, string? serialNum = null, string? chipUid = null, CancellationToken ct = default);
        Task RejectJobAsync(long jobId, string reason, CancellationToken ct = default);
        Task<List<CardJob>> GetJobsByTransactionIdAsync(long transactionId, CancellationToken ct = default);
    }
}
