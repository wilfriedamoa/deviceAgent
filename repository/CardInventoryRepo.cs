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
    internal class CardInventoryRepo(IDbContextFactory<AgentDbContext> dbContextFactory) : ICardInventory
    {
        private readonly IDbContextFactory<AgentDbContext> _dbContextFactory = dbContextFactory;

        public async Task<CardInventory?> GetActiveInventoryForDispenseAsync(CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            return await db.CardInventories
                .FirstOrDefaultAsync(i => i.IsActive && i.RemainingQuantity > 0, ct);
        }

        public async Task<List<CardInventory>> GetAllInventoriesAsync(CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            return await db.CardInventories.ToListAsync(ct);
        }

        public async Task DecrementStockAsync(int feederCassetteNo, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var inventory = await db.CardInventories
                .FirstOrDefaultAsync(i => i.FeederCassetteNo == feederCassetteNo, ct);

            if (inventory != null && inventory.RemainingQuantity > 0)
            {
                inventory.RemainingQuantity--;
                inventory.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
