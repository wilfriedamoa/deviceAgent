using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    internal record DriverResult
    (
        bool Success, string ErrorCode, string StatusMessage
    );
}
