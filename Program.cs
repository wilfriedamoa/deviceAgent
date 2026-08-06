using deviceAgent;
using deviceAgent.data;
using deviceAgent.drivers;
using deviceAgent.repository;
using deviceAgent.services;
using deviceAgent.services.grpc;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

//var builder = Host.CreateApplicationBuilder(args);
var builder = WebApplication.CreateBuilder(args);
// 1. Ajouter le service Windows pour exécuter l'application en tant que service
builder.Services.AddWindowsService(options=>
{
    options.ServiceName = "Device Agent Service";
});

// Configuration du serveur Kestrel pour exposer gRPC sur le port interne 50051 (HTTP/2)
// Configuration du serveur Kestrel pour exposer gRPC sur le port interne 50051 (HTTP/2)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(50051, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// 2. Définir le chemin de la base SQLite dans AppData local (pour éviter les problèmes de droits Admin)
string appDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "DEVICEAGENT"
);
Directory.CreateDirectory(appDataFolder); // Crée le dossier si nécessaire
string dbPath = Path.Combine(appDataFolder, "deviceagent.db");
// 3. Ajouter le service DbContext avec le chemin de la base SQLite
builder.Services.AddDbContextFactory<AgentDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
    #if DEBUG
    options.EnableSensitiveDataLogging(); // Pour le débogage, à retirer en production
    options.EnableDetailedErrors(); // Pour le débogage, à retirer en production
    #endif
});

//4. Enregistrer les Repositories et Services Métier
builder.Services.AddSingleton<TransactionRepo>();
builder.Services.AddSingleton<AuditLogRepo>();
builder.Services.AddSingleton<CardInventoryRepo>();
builder.Services.AddSingleton<CardJobRepo>();
builder.Services.AddSingleton<DeviceStatusRepo>();
builder.Services.AddSingleton<EventQueueRepo>();

// =========================================================================
// 4.1 ENREGISTREMENT DES DRIVERS MATÉRIELS (SINGLETONS)
// =========================================================================
builder.Services.AddSingleton<EvolisCardPrinterDriver>();
// =========================================================================
// 4.2 ENREGISTREMENT DES SERVICES MÉTIER & DISPATCHER
// =========================================================================
builder.Services.AddSingleton<IPrinterService, PrinterService>();
builder.Services.AddSingleton<IHealthService, HealthService>();
builder.Services.AddSingleton<ICardDispenserService, CardDispenseService>();
builder.Services.AddSingleton<ITransactionService, TransactionService>();
builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();

// 5. Enregistrer le Worker d'arrière-plan pour dépiler l'EventQueue
//builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<EventQueueProcessor>();

var host = builder.Build();

// 6. INITIALISATION AU DÉMARRAGE : Migrations & SQLite PRAGMAs
using (var scope = host.Services.CreateScope())
{
    //var services = scope.ServiceProvider;
    //var dbContextFactory = services.GetRequiredService<IDbContextFactory<AgentDbContext>>();
    //using var dbContext = await dbContextFactory.CreateDbContextAsync();
    //await dbContext.Database.MigrateAsync(); // Applique les migrations si nécessaire
    //await AgentDbContext.ApplyKioskSqlitePragmasAsync(dbContext); // Applique les optimisations SQLite

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AgentDbContext>>();

    try
    {
        logger.LogInformation("Initialisation de la base de données SQLite à : {DbPath}", dbPath);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        // Crée la BDD et applique le schéma si elle n'existe pas
        await dbContext.Database.EnsureCreatedAsync();

        // Applique les PRAGMAs indispensables pour le mode borne (WAL, timeouts, FKs)
        await AgentDbContext.ApplyKioskSqlitePragmasAsync(dbContext);

        logger.LogInformation("Base de données SQLite initialisée et configurée avec succès (Mode WAL).");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Échec lors de l'initialisation de la base de données SQLite.");
        throw; // Empêche le service de démarrer en état instable
    }
}

string cmsBaseUrl = builder.Configuration.GetValue<string>("BackendConfig:BaseUrl")
    ?? "http://127.0.0.1:8080/api";

//int requestTimeoutSeconds = builder.Configuration.GetValue<int>("CmsConfiguration:TimeoutSeconds", 15);

// Configuration d'un HttpClient dédié injecté spécifiquement dans EventQueueProcessor
builder.Services.AddHttpClient<EventQueueProcessor>(client =>
{
    client.BaseAddress = new Uri(cmsBaseUrl);
    //client.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("X-Kiosk-Agent-Version", "1.0.0");
    // Optionnel : Token d'API ou clé de borne pour l'authentification au CMS
    string apiKey = builder.Configuration.GetValue<string>("BackendConfig:ApiKey") ?? string.Empty;
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }
});

// =========================================================================
// 7. SERVICES DE INFRASTRUCTURE (gRPC & WORKERS EN ARRIÈRE-PLAN)
// =========================================================================
builder.Services.AddGrpc();

// =========================================================================
// 8. ROUTAGE DES ENDPOINTS gRPC ET DÉMARRAGE
// =========================================================================
host.MapGrpcService<PrinterGrpcService>();
host.MapGrpcService<HealthGrpcService>();

host.MapGet("/", () => "deviceAgent Service gRPC opérationnel sur localhost:50051");
await host.RunAsync(); // Utilisation de RunAsync pour permettre l'annulation via Ctrl+C ou SIGTERM
