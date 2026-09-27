namespace Reflow.Modules.DataProcessing.Abstractions;

/// <summary>Parses raw text into frames. Enforces row/size caps.</summary>
public interface IDataReader
{
    Frame FromCsv(string text, char delimiter = ',', bool hasHeader = true);
    Frame FromJson(string json, string? rootPath = null);
}
