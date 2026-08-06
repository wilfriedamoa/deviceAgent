using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.interfaces
{
    internal interface IAuditLog
    {
        Task LogAsync(string component, string action, string statusCode, string? details = null, CancellationToken ct = default);
    }
}
