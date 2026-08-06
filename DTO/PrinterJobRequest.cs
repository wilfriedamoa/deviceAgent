using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    internal record PrinterJobRequest
    (
        string JobId,
        string PrintType,
        string Payload
    );
}
