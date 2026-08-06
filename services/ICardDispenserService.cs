using deviceAgent.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal interface ICardDispenserService
    {
        Task<CardIssuanceResult> ProcessCardDispenseAsync(long transactionId, CardHolderData cardDataPayload,ChipType chipType ,CancellationToken ct = default);
    }
}
