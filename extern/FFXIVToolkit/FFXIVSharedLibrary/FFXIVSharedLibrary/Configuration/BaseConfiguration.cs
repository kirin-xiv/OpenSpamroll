using System.Text.Json;
using System.Reflection;

namespace FFXIVSharedLibrary.Configuration;

public interface IBaseConfiguration
{
    int Version { get; set; }
    void Save();
    void Load();
}

public interface IDebugConfiguration : IBaseConfiguration
{
    bool DebugMode { get; set; }
}

[Serializable]
public abstract class BaseConfiguration : IBaseConfiguration
{
    public virtual int Version { get; set; } = 1;

    [NonSerialized]
    protected object? configurationManager;

    public virtual void Initialize(object? configurationManager = null)
    {
        this.configurationManager = configurationManager;
    }

    public abstract void Save();
    public abstract void Load();

    protected virtual void OnVersionChanged(int oldVersion, int newVersion)
    {
    }

    public virtual T GetValue<T>(string key, T defaultValue = default!)
    {
        var property = GetType().GetProperty(key);
        if (property == null) return defaultValue;

        var value = property.GetValue(this);
        if (value is T typedValue) return typedValue;
        if (value == null) return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public virtual void SetValue<T>(string key, T value)
    {
        var property = GetType().GetProperty(key);
        if (property != null && property.CanWrite)
        {
            var oldValue = property.GetValue(this);
            property.SetValue(this, value);
            
            // Handle debug mode changes
            if (key == "DebugMode" && this is IDebugConfiguration && oldValue != null && !oldValue.Equals(value))
            {
                OnDebugModeChanged((bool)(object)value!);
            }
        }
    }

    protected virtual void OnDebugModeChanged(bool enabled)
    {
        // Subclasses can override this to handle debug mode changes
    }

    public virtual Dictionary<string, object?> GetAllValues()
    {
        var values = new Dictionary<string, object?>();
        var properties = GetType().GetProperties();

        foreach (var property in properties)
        {
            if (property.CanRead && !property.GetCustomAttributes(typeof(NonSerializedAttribute), true).Any())
            {
                values[property.Name] = property.GetValue(this);
            }
        }

        return values;
    }
}

public class JsonFileConfiguration : BaseConfiguration
{
    private readonly string filePath;
    private readonly JsonSerializerOptions jsonOptions;

    public JsonFileConfiguration(string filePath)
    {
        this.filePath = filePath;
        this.jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public override void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, GetType(), jsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration to {filePath}", ex);
        }
    }

    public override void Load()
    {
        try
        {
            if (!File.Exists(filePath))
                return;

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var loaded = JsonSerializer.Deserialize(json, GetType(), jsonOptions);
            if (loaded == null) return;

            var properties = GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.CanWrite && !property.GetCustomAttributes(typeof(NonSerializedAttribute), true).Any())
                {
                    var value = property.GetValue(loaded);
                    property.SetValue(this, value);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration from {filePath}", ex);
        }
    }

    public string GetFilePath() => filePath;
}

public class MemoryConfiguration : BaseConfiguration
{
    private readonly Dictionary<string, object?> storage = new();

    public override void Save()
    {
        storage.Clear();
        var properties = GetType().GetProperties();

        foreach (var property in properties)
        {
            if (property.CanRead && !property.GetCustomAttributes(typeof(NonSerializedAttribute), true).Any())
            {
                storage[property.Name] = property.GetValue(this);
            }
        }
    }

    public override void Load()
    {
        var properties = GetType().GetProperties();

        foreach (var property in properties)
        {
            if (property.CanWrite && storage.ContainsKey(property.Name))
            {
                property.SetValue(this, storage[property.Name]);
            }
        }
    }

    public Dictionary<string, object?> GetStorage() => new(storage);
    public void ClearStorage() => storage.Clear();
}

[Serializable]
public abstract class DebugConfiguration : BaseConfiguration, IDebugConfiguration
{
    private bool debugMode = false;

    public virtual bool DebugMode 
    { 
        get => debugMode;
        set
        {
            if (debugMode != value)
            {
                debugMode = value;
                OnDebugModeChanged(value);
            }
        }
    }
}

public class JsonFileDebugConfiguration : DebugConfiguration
{
    private readonly string filePath;
    private readonly JsonSerializerOptions jsonOptions;

    public JsonFileDebugConfiguration(string filePath)
    {
        this.filePath = filePath;
        this.jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected JsonFileDebugConfiguration()
    {
        this.filePath = string.Empty;
        this.jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public override void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, GetType(), jsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save debug configuration to {filePath}", ex);
        }
    }

    public override void Load()
    {
        try
        {
            if (!File.Exists(filePath))
                return;

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            // Use a dictionary-based approach to avoid constructor issues
            var jsonDocument = JsonDocument.Parse(json);
            foreach (var element in jsonDocument.RootElement.EnumerateObject())
            {
                var property = GetType().GetProperty(element.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property != null && property.CanWrite)
                {
                    var value = JsonSerializer.Deserialize(element.Value.GetRawText(), property.PropertyType, jsonOptions);
                    property.SetValue(this, value);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load debug configuration from {filePath}", ex);
        }
    }

    public string GetFilePath() => filePath;
}