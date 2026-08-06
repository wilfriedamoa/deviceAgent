using deviceAgent.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal interface ICommandDispatcher
    {
        Task<CardIssuanceResult> DispatchPersonalizedCardIssuanceAsync(long transactionId,
         CardHolderData cardData,
         ChipType chipType,
         CancellationToken ct = default);
    }
}
