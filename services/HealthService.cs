using deviceAgent.data;
using deviceAgent.drivers;
using deviceAgent.DTO;
using deviceAgent.repository;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deviceAgent.services
{
    internal class HealthService(ILogger<HealthService> logger,EvolisCardPrinterDriver evolisCardPrinterDriver,DeviceStatusRepo deviceStatusRepo,IDbContextFactory<AgentDbContext> dbContextFactory):IHealthService
    {
        private readonly ILogger<HealthService> _logger = logger;
        private readonly EvolisCardPrinterDriver _cardPrinterDriver = evolisCardPrinterDriver;
        private readonly DeviceStatusRepo _deviceStatusRepo = deviceStatusRepo;
        private readonly IDbContextFactory<AgentDbContext> _dbContextFactory = dbContextFactory;


        public async Task<SystemHealthReport> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var components = new List<ComponentHealth>();

            // 1. Diagnostics Imprimante USB
            bool printerConnected = _cardPrinterDriver.IsConnected;
            components.Add(new ComponentHealth(
                ComponentName: "EvolisCardPrinter",
                IsConnected: printerConnected,
                Level: printerConnected ? HealthLevel.Ok : HealthLevel.Critical,
                Details: printerConnected ? "Périphérique opérationnel" : "Imprimante déconnectée"
            ));

            // 2. Diagnostic SQLite
            bool dbHealthy = false;
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                dbHealthy = await db.Database.CanConnectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur d'accès à la BDD SQLite");
            }

            components.Add(new ComponentHealth(
                ComponentName: "SQLiteLocalDb",
                IsConnected: dbHealthy,
                Level: dbHealthy ? HealthLevel.Ok : HealthLevel.Critical,
                Details: dbHealthy ? "Base accessible (Mode WAL)" : "Fichier BDD inaccessible"
            ));

            // 3. Ressources système (RAM & Disque C:)
            var process = Process.GetCurrentProcess();
            double ramUsageMb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2);

            DriveInfo systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            double freeSpaceGb = Math.Round(systemDrive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 2);

            HealthLevel globalHealth = (printerConnected && dbHealthy) ? HealthLevel.Ok : HealthLevel.Warning;
            if (freeSpaceGb < 2.0) globalHealth = HealthLevel.Critical;

            var report = new SystemHealthReport(
                GlobalHealth: globalHealth,
                CpuUsagePercent: 0.0,
                RamUsageMb: ramUsageMb,
                DiskFreeSpaceGb: freeSpaceGb,
                Components: components,
                Timestamp: DateTime.UtcNow
            );

            // 4. Persistance de l'état des composants
            foreach (var comp in components)
            {
                await _deviceStatusRepo.UpsertStatusAsync(
                    comp.ComponentName,
                    comp.IsConnected,
                    comp.IsConnected ? "READY" : "OFFLINE",
                    comp.Level.ToString().ToUpper(),
                    comp.Details,
                    cancellationToken
                );
            }

            return report;
        }

    }
}
