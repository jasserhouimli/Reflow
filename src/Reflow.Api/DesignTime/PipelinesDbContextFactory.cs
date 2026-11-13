using Reflow.Modules.Pipelines.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reflow.Api.DesignTime;

public class PipelinesDbContextFactory : IDesignTimeDbContextFactory<PipelinesDbContext>
{
    public PipelinesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Reflow")
            ?? "Host=localhost;Port=5432;Database=reflow;Username=postgres;Password=root";

        var optionsBuilder = new DbContextOptionsBuilder<PipelinesDbContext>();
        optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly("Reflow.Api"));
        return new PipelinesDbContext(optionsBuilder.Options);
    }
}
