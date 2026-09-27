using Reflow.Modules.Triggers.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reflow.Api.DesignTime;

public class TriggersDbContextFactory : IDesignTimeDbContextFactory<TriggersDbContext>
{
    public TriggersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TriggersDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=reflow;Username=postgres;Password=root",
            b => b.MigrationsAssembly("Reflow.Api"));
        return new TriggersDbContext(optionsBuilder.Options);
    }
}
