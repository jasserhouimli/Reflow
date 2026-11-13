using Reflow.Modules.PipelineExecution.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reflow.Api.DesignTime;

public class PipelineExecutionDbContextFactory : IDesignTimeDbContextFactory<PipelineExecutionDbContext>
{
    public PipelineExecutionDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Reflow")
            ?? "Host=localhost;Port=5432;Database=reflow;Username=postgres;Password=root";

        var optionsBuilder = new DbContextOptionsBuilder<PipelineExecutionDbContext>();
        optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly("Reflow.Api"));
        return new PipelineExecutionDbContext(optionsBuilder.Options);
    }
}
