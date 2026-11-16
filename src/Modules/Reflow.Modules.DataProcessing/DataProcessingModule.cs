using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.DataProcessing.InMemory;

namespace Reflow.Modules.DataProcessing;

public static class DataProcessingModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IDataReader, CsvCodec>();
        builder.Services.AddSingleton<IDataWriter, FrameWriter>();

        builder.Services.Configure<StorageOptions>(
            builder.Configuration.GetSection(StorageOptions.Section));

        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<IDataArtifactStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var writer = sp.GetRequiredService<IDataWriter>();
            var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<SupabaseArtifactStore>>();
            var localFallback = new LocalArtifactStore(writer);

            if (string.Equals(options.Provider, "Supabase", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(options.SupabaseUrl) && !string.IsNullOrWhiteSpace(options.SupabaseKey)))
            {
                return new SupabaseArtifactStore(clientFactory, writer, Options.Create(options), localFallback, logger);
            }

            return localFallback;
        });

        builder.Services.AddSingleton<ISqlEngine, DuckDbSqlEngine>();
    }
}
