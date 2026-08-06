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
    internal class DeviceStatusRepo(IDbContextFactory<AgentDbContext> dbContextFactory) : IDeviceStatus
    {
        private readonly IDbContextFactory<AgentDbContext> _dbContextFactory = dbContextFactory;

        public async Task UpsertStatusAsync(string componentName, bool isOnline, string statusCode, string healthState, string? detailsJson = null, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var status = await db.DeviceStatuses.FindAsync(new object[] { componentName }, ct);
            if (status == null)
            {
                status = new DeviceStatus { ComponentName = componentName };
                db.DeviceStatuses.Add(status);
            }

            status.IsOnline = isOnline;
            status.StatusCode = statusCode;
            status.HealthState = healthState;
            status.DetailsJson = detailsJson;
            status.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
        }

        public async Task<List<DeviceStatus>> GetAllStatusesAsync(CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            return await db.DeviceStatuses.ToListAsync(ct);
        }
    }
}
