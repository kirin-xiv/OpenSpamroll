using System.Text.Json;

namespace FFXIVSharedLibrary.Build;

public static class VersioningHelper
{
    public static void UpdateJsonVersion(string jsonFilePath, string version)
    {
        if (!File.Exists(jsonFilePath))
            throw new FileNotFoundException($"JSON file not found: {jsonFilePath}");

        try
        {
            var jsonContent = File.ReadAllText(jsonFilePath);
            var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;

            // Create a dictionary from the JSON
            var jsonDict = new Dictionary<string, object?>();
            foreach (var property in root.EnumerateObject())
            {
                jsonDict[property.Name] = GetJsonValue(property.Value);
            }

            // Update the AssemblyVersion
            jsonDict["AssemblyVersion"] = version;

            // Serialize back to JSON with formatting
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null // Preserve original casing
            };

            var updatedJson = JsonSerializer.Serialize(jsonDict, options);
            File.WriteAllText(jsonFilePath, updatedJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update version in {jsonFilePath}: {ex.Message}", ex);
        }
    }

    public static void UpdateMultipleJsonVersions(IEnumerable<string> jsonFilePaths, string version)
    {
        var errors = new List<string>();

        foreach (var filePath in jsonFilePaths)
        {
            if (!File.Exists(filePath))
                continue;

            try
            {
                UpdateJsonVersion(filePath, version);
            }
            catch (Exception ex)
            {
                errors.Add($"{filePath}: {ex.Message}");
            }
        }

        if (errors.Any())
        {
            throw new AggregateException($"Failed to update some JSON files:\n{string.Join("\n", errors)}");
        }
    }

    public static string? GetVersionFromJson(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
            return null;

        try
        {
            var jsonContent = File.ReadAllText(jsonFilePath);
            var jsonDocument = JsonDocument.Parse(jsonContent);
            
            if (jsonDocument.RootElement.TryGetProperty("AssemblyVersion", out var versionElement))
            {
                return versionElement.GetString();
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    public static Dictionary<string, string?> GetVersionsFromMultipleJson(IEnumerable<string> jsonFilePaths)
    {
        var versions = new Dictionary<string, string?>();

        foreach (var filePath in jsonFilePaths)
        {
            var fileName = Path.GetFileName(filePath);
            versions[fileName] = GetVersionFromJson(filePath);
        }

        return versions;
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => GetJsonObject(element),
            JsonValueKind.Array => GetJsonArray(element),
            _ => element.ToString()
        };
    }

    private static Dictionary<string, object?> GetJsonObject(JsonElement element)
    {
        var obj = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject())
        {
            obj[property.Name] = GetJsonValue(property.Value);
        }
        return obj;
    }

    private static List<object?> GetJsonArray(JsonElement element)
    {
        var array = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            array.Add(GetJsonValue(item));
        }
        return array;
    }
}