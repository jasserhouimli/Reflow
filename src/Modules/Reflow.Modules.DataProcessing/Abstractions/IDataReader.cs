namespace Reflow.Modules.DataProcessing.Abstractions;

// Parses raw tabular text (CSV, JSON) into in-memory Frames.
public interface IDataReader
{
    Frame FromCsv(string text, char delimiter = ',', bool hasHeader = true);
    Frame FromJson(string json, string? rootPath = null);
}
