namespace Reflow.Modules.DataProcessing.Abstractions;

/// <summary>Serializes frames. Parquet writer arrives separately.</summary>
public interface IDataWriter
{
    string ToJson(Frame frame);
    string ToCsv(Frame frame, char delimiter = ',', bool includeHeader = true);
}
