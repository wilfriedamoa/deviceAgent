using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.DTO
{
    internal enum HealthLevel
    {
        Ok,
        Warning,
        Critical
    }

    internal record ComponentHealth(
            string ComponentName,
             bool IsConnected,
            HealthLevel Level,
             string Details
        );

    internal record SystemHealthReport(
            HealthLevel GlobalHealth,
            double CpuUsagePercent,
            double RamUsageMb,
            double DiskFreeSpaceGb,
            IReadOnlyList<ComponentHealth> Components,
             DateTime Timestamp
    );
}
