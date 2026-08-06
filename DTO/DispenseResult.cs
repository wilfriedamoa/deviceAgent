using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    internal record DispenseResult
    (
        bool Success, string? CardSerialNumber, string? ChipUid, string? ErrorCode, string? Message

    );
    
}
