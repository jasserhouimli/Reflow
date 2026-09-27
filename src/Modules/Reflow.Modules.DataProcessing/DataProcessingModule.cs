using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.DataProcessing.DataQuality;
using Reflow.Modules.DataProcessing.Datasets;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.DataProcessing.Lineage;

namespace Reflow.Modules.DataProcessing;

public static class DataProcessingModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IDataReader, CsvCodec>();
        builder.Services.AddSingleton<IDataWriter, FrameWriter>();
        builder.Services.AddSingleton<IDataArtifactStore, LocalArtifactStore>();
        builder.Services.AddSingleton<IDatasetCatalog, InMemoryDatasetCatalog>();
        builder.Services.AddSingleton<IDataProfiler, FrameProfiler>();
        builder.Services.AddSingleton<ILineageStore, InMemoryLineageStore>();
        builder.Services.AddSingleton<ISqlEngine, DuckDbSqlEngine>();
    }
}
