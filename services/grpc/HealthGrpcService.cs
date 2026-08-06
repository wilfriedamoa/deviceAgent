using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services.grpc
{
    internal class HealthGrpcService(ILogger<HealthGrpcService> logger,IHealthService healthService) :HealthGrpc.HealthGrpcBase
    {
        private readonly ILogger<HealthGrpcService> _logger = logger;
        private readonly IHealthService _healthService = healthService;

        public override async Task<HealthReportResponse> CheckHealth(EmptyRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Requête gRPC reçue: CheckHealth");

            var report = await _healthService.PerformHealthCheckAsync(context.CancellationToken);

            var response = new HealthReportResponse
            {
                GlobalHealth = report.GlobalHealth.ToString().ToUpper(),
                CpuUsagePercent = report.CpuUsagePercent,
                RamUsageMb = report.RamUsageMb,
                DiskFreeSpaceGb = report.DiskFreeSpaceGb,
                Timestamp = report.Timestamp.ToString("o")
            };

            response.Components.AddRange(report.Components.Select(c => new ComponentHealthDto
            {
                ComponentName = c.ComponentName,
                IsConnected = c.IsConnected,
                Level = c.Level.ToString().ToUpper(),
                Details = c.Details
            }));

            return response;
        }
    }
}
