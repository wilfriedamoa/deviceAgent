using deviceAgent.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.interfaces
{
    internal interface ICardInventory
    {
        Task<CardInventory?> GetActiveInventoryForDispenseAsync(CancellationToken ct = default);
        Task<List<CardInventory>> GetAllInventoriesAsync(CancellationToken ct = default);
        Task DecrementStockAsync(int feederCassetteNo, CancellationToken ct = default);
    }
}
