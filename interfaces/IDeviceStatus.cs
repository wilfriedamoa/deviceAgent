using deviceAgent.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.interfaces
{
    internal interface IDeviceStatus
    {
        Task UpsertStatusAsync(string componentName, bool isOnline, string statusCode, string healthState, string? detailsJson = null, CancellationToken ct = default);
        Task<List<DeviceStatus>> GetAllStatusesAsync(CancellationToken ct = default);
    }
}
