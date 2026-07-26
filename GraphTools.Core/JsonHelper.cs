using System.Text.Json;

namespace GraphTools.Core;

public static class JsonHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void WriteToFile<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(path, json);
    }

    public static T ReadFromFile<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from {path}");
    }
}
