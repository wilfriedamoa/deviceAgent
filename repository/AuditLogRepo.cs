using deviceAgent.data;
using deviceAgent.interfaces;
using deviceAgent.model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.repository
{
    internal class AuditLogRepo(IDbContextFactory<AgentDbContext> dbContextFactory) : IAuditLog
    {
        private readonly IDbContextFactory<AgentDbContext> _dbContextFactory = dbContextFactory;
        public async Task LogAsync(string component, string action, string statusCode, string? details = null, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            db.AuditLogs.Add(new AuditLog
            {
                Component = component,
                Action = action,
                StatusCode = statusCode,
                Details = details,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
        }
    }
}
