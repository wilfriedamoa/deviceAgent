using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    internal class PrinterEventArgs : EventArgs
    {
        public required string JobId { get; init; }
        public required DevicePrinterStatus Status { get; init; }
        public required string Message { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
