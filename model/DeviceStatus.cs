using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.model
{
    internal class DeviceStatus : BaseM
    {
        public string ComponentName { get; set; } = string.Empty; // PK
        public bool IsOnline { get; set; }
        public string StatusCode { get; set; } = "UNKNOWN";
        public string HealthState { get; set; } = "OK"; // OK, WARNING, CRITICAL
        public string? DetailsJson { get; set; }
      
    }
}
