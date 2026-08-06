using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.model
{
    internal class AuditLog : BaseM
    {
        public string Component { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}
