namespace Reflow.Modules.DataProcessing.Abstractions;

// Serializes Frames into text formats (JSON, CSV).
public interface IDataWriter
{
    string ToJson(Frame frame);
    string ToCsv(Frame frame, char delimiter = ',', bool includeHeader = true);
}
