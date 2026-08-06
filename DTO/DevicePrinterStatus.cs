using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    internal enum DevicePrinterStatus
    {
        Ready,
        Busy,
        OutOfPaper, // Ou OutOfCards
        Jammed,
        Offline,
        Error
    }
}
