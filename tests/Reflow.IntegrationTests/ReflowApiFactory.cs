using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Reflow.Modules.Identity.Persistence;
using Reflow.Modules.PipelineExecution.Persistence;
using Reflow.Modules.Pipelines.Persistence;
using Reflow.Modules.Triggers.Persistence;
using Xunit;

namespace Reflow.IntegrationTests;

public class ReflowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestConnectionString =
        "Host=localhost;Port=5432;Database=reflow_test;Username=postgres;Password=root";

    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=root";

    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _initialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Reflow", TestConnectionString);
        builder.UseSetting("Jwt:Key", "IntegrationTestSecretKey0123456789ABCDEF0123456789ABCDEF");
        builder.UseSetting("Jwt:Issuer", "Reflow");
        builder.UseSetting("Jwt:Audience", "Reflow");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
        builder.UseSetting("RateLimiting:GeneralPermitLimit", "10000");
        builder.UseSetting("WorkflowExecution:AllowPrivateNetwork", "false");
        builder.UseSetting("Triggers:TickSeconds", "2");
    }

    public async Task InitializeAsync()
    {
        // NOTE: this runs before the test host starts, so the worker can never
        // observe a missing schema. DbContexts are constructed directly instead
        // of using Services, which would start the host prematurely.
        await InitLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            await using var admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync();
            await using (var check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname='reflow_test'", admin))
            {
                if (await check.ExecuteScalarAsync() is null)
                {
                    await using var create = new NpgsqlCommand("CREATE DATABASE reflow_test", admin);
                    await create.ExecuteNonQueryAsync();
                }
            }

            // NOTE: DbContext.Database.EnsureCreated() is a no-op when the database
            // already contains any tables, so with three contexts sharing one
            // database only the first call would take effect. CreateTables()
            // per context instead, guarded by an existence check.
            using (var identity = new IdentityDbContext(Options<IdentityDbContext>()))
                EnsureSchema(identity, "identity", "users");
            using (var pipelines = new PipelinesDbContext(Options<PipelinesDbContext>()))
                EnsureSchema(pipelines, "pipelines", "Pipelines");
            using (var execution = new PipelineExecutionDbContext(Options<PipelineExecutionDbContext>()))
                EnsureSchema(execution, "pipeline_execution", "PipelineRuns");
            using (var triggers = new TriggersDbContext(Options<TriggersDbContext>()))
            {
                EnsureSchema(triggers, "triggers", "Triggers");
                EnsureSchema(triggers, "triggers", "WebhookEvents");
            }

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static DbContextOptions<T> Options<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql(TestConnectionString).Options;

    private static void EnsureSchema(DbContext db, string schema, string table)
    {
        var exists = db.Database
            .SqlQueryRaw<int>("SELECT 1 FROM pg_tables WHERE schemaname = {0} AND tablename = {1}", schema, table)
            .ToList().Count > 0;
        if (!exists)
            db.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}
