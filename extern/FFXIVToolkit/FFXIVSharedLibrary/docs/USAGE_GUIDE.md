# 📚 FFXIVSharedLibrary Usage Guide

> **Complete guide** to using all components of the FFXIVSharedLibrary in your FFXIV plugins.

---

## 🚀 **Getting Started**

### **1. Add Library Reference**

Add this to your `.csproj` file:

```xml
<ItemGroup>
    <ProjectReference Include="path\to\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
</ItemGroup>
```

### **2. Add Using Statements**

```csharp
using FFXIVSharedLibrary.Player;      // Player name normalization, server data
using FFXIVSharedLibrary.Chat;        // Chat processing, roll handling
using FFXIVSharedLibrary.GameState;   // Game session management
using FFXIVSharedLibrary.Configuration; // Configuration helpers
using FFXIVSharedLibrary.Build;       // Build-time versioning utilities
```

---

## 🔧 **Automated Versioning**

### **How It Works**
When you build your project, the library automatically updates JSON files with your current version.

### **Setup**
```xml
<!-- In your .csproj -->
<PropertyGroup>
    <Version>1.5.0.0</Version>  <!-- Change this to update everywhere -->
    <AssemblyVersion>$(Version)</AssemblyVersion>
    <FileVersion>$(Version)</FileVersion>
</PropertyGroup>

<ItemGroup>
    <ProjectReference Include="path\to\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
</ItemGroup>
<!-- Versioning happens automatically! -->
```

### **Supported JSON Files**
- `YourPlugin.json` (based on AssemblyName)
- `Plugin.json` (standard Dalamud)
- `FFToD.json` (legacy format)

### **Custom Configuration**
```xml
<!-- Override which files get updated -->
<PropertyGroup>
    <VersioningJsonFiles>$(ProjectDir)MyPlugin.json;$(ProjectDir)OtherFile.json</VersioningJsonFiles>
    <EnableAutoVersioning>true</EnableAutoVersioning>
</PropertyGroup>
```

### **Programmatic Usage**
```csharp
using FFXIVSharedLibrary.Build;

// Update a single file
VersioningHelper.UpdateJsonVersion("Plugin.json", "2.1.0.0");

// Update multiple files
var files = new[] { "Plugin.json", "Manifest.json" };
VersioningHelper.UpdateMultipleJsonVersions(files, "2.1.0.0");

// Get current version from JSON
var version = VersioningHelper.GetVersionFromJson("Plugin.json");
```

---

## 🎭 **Player Management**

### **Player Name Normalization**

```csharp
using FFXIVSharedLibrary.Player;

// Remove server suffixes
var cleanName = PlayerNameNormalizer.NormalizeName("Kirin Blackthorne Gilgamesh");
// Result: "Kirin Blackthorne"

// Handle "You" case with local player name
var playerName = PlayerNameNormalizer.NormalizeNameWithLocalPlayer(
    "You", 
    clientState.LocalPlayer?.Name.TextValue
);
// Result: Your actual character name
```

### **Server Data Utilities**

```csharp
// Check if a server exists
bool isValid = ServerData.IsValidServer("Gilgamesh"); // true
bool isFake = ServerData.IsValidServer("FakeServer");  // false

// Get server information
string? datacenter = ServerData.GetDatacenterForServer("Gilgamesh"); // "Aether"
string? region = ServerData.GetRegionForServer("Gilgamesh"); // "North America"

// Get all servers in a datacenter
var aetherServers = ServerData.GetServersInDatacenter("Aether");
// Returns: HashSet with all Aether servers

// Get all servers in a region
var naServers = ServerData.GetServersInRegion("North America");
// Returns: HashSet with all NA servers

// Access the complete server list
var allServers = ServerData.AllServers; // HashSet<string> with 85+ servers
```

---

## 💬 **Chat Processing**

### **Basic Chat Message Processing**

```csharp
using FFXIVSharedLibrary.Chat;

public class MyPlugin : IDalamudPlugin
{
    private readonly ChatMessageProcessor chatProcessor;
    
    public MyPlugin(IChatGui chatGui)
    {
        chatProcessor = new ChatMessageProcessor();
        
        // Connect to Dalamud's chat events
        chatGui.ChatMessage += OnChatMessage;
    }
    
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        chatProcessor.ProcessMessage((int)type, timestamp, sender.TextValue, message.TextValue);
    }
}
```

### **Roll Detection and Handling**

```csharp
using FFXIVSharedLibrary.Chat;

// Set up roll detection
var rollHandler = new RollHandler(clientState.LocalPlayer?.Name.TextValue);
var rollCollector = new RollCollector();

// Register the roll handler
chatProcessor.RegisterHandler(rollHandler);

// Handle detected rolls
rollHandler.RollDetected += (rollArgs) => {
    if (rollCollector.AddRoll(rollArgs)) // Returns false if player already rolled
    {
        chatGui.Print($"[Game] {rollArgs.NormalizedPlayerName} rolled {rollArgs.RollValue}!");
    }
    else
    {
        chatGui.Print($"[Game] {rollArgs.NormalizedPlayerName} already rolled!");
    }
};

// Get roll results
var highestRoll = rollCollector.GetHighestRoll();
if (highestRoll.HasValue)
{
    chatGui.Print($"Winner: {highestRoll.Value.playerName} with {highestRoll.Value.rollValue}!");
}

// Clear rolls for next round
rollCollector.ClearRolls();
```

### **Custom Chat Handlers**

```csharp
// Create custom handler for specific chat patterns
public class MyCustomHandler : RegexChatHandler
{
    public override int Priority => 100; // Higher = processed first
    
    public MyCustomHandler() : base(@"!mycommand (.+)", RegexOptions.IgnoreCase) 
    {
    }
    
    public override void Handle(ChatMessageEventArgs args)
    {
        var match = GetMatch(args);
        var command = match.Groups[1].Value;
        
        // Process your custom command
        Console.WriteLine($"Got command: {command}");
        
        // Optionally stop further processing
        args.IsHandled = true;
    }
}

// Register your custom handler
chatProcessor.RegisterHandler(new MyCustomHandler());
```

---

## 🎮 **Game Session Management**

### **Basic Game Sessions**

```csharp
using FFXIVSharedLibrary.GameState;

// Define your game state
public class MyGameState
{
    public Dictionary<string, int> PlayerRolls { get; set; } = new();
    public string CurrentWinner { get; set; } = "";
    public int RoundNumber { get; set; } = 1;
}

// Create session manager
var gameSession = new GameSessionManager<MyGameState>();

// Handle state changes
gameSession.StateChanged += (oldState, newState, gameState) => {
    chatGui.Print($"[Game] {oldState} → {newState}");
};

gameSession.SessionCompleted += (gameState, winner) => {
    chatGui.Print($"[Game] Session completed! Winner: {winner ?? "None"}");
};

// Start a game session
if (gameSession.StartSession(new MyGameState()))
{
    chatGui.Print("[Game] Session started!");
}

// Update game state safely
gameSession.UpdateGameState(state => {
    state.PlayerRolls["PlayerName"] = 42;
    state.RoundNumber++;
});

// Get values from game state
var roundNumber = gameSession.GetGameStateValue(state => state.RoundNumber);

// Complete the session
gameSession.CompleteSession("WinnerName");
```

### **Timed Game Sessions**

```csharp
// Auto-timeout after specified duration
var timedSession = new TimedGameSessionManager<MyGameState>(TimeSpan.FromMinutes(5));

// Start with custom timeout
timedSession.StartSession(new MyGameState(), TimeSpan.FromSeconds(30));

// Session will automatically stop after timeout
```

### **Session with Manual Timeout**

```csharp
// Start session with timeout
if (gameSession.StartSessionWithTimeout(TimeSpan.FromSeconds(30)))
{
    chatGui.Print("[Game] 30-second rolling phase started!");
}

// Session will automatically stop after 30 seconds
```

---

## ⚙️ **Configuration Management**

### **JSON File Configuration**

```csharp
using FFXIVSharedLibrary.Configuration;

// Create configuration class
public class MyPluginConfig : JsonFileConfiguration
{
    public int RollTimeout { get; set; } = 30;
    public string LastWinner { get; set; } = "";
    public bool AutoStartEnabled { get; set; } = true;
    public List<string> BannedPlayers { get; set; } = new();
    
    public MyPluginConfig() : base(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyPlugin", "config.json"))
    {
        Load(); // Load existing config on startup
    }
}

// Usage
var config = new MyPluginConfig();

// Modify settings
config.RollTimeout = 45;
config.BannedPlayers.Add("BadPlayer");

// Save changes
config.Save(); // Persists to JSON file

// Dynamic property access
config.SetValue("RollTimeout", 60);
var timeout = config.GetValue<int>("RollTimeout", 30); // 30 = default if not found
```

### **Memory-Only Configuration**

```csharp
// For temporary settings that don't need persistence
public class TempConfig : MemoryConfiguration
{
    public Dictionary<string, object> SessionData { get; set; } = new();
    public bool DebugMode { get; set; } = false;
}

var tempConfig = new TempConfig();
tempConfig.DebugMode = true;
tempConfig.Save(); // Stores in memory only
```

---

## 🎯 **Complete Example: Mini-Game Plugin**

```csharp
using FFXIVSharedLibrary.Player;
using FFXIVSharedLibrary.Chat;
using FFXIVSharedLibrary.GameState;
using FFXIVSharedLibrary.Configuration;

public class DiceGamePlugin : IDalamudPlugin
{
    public string Name => "Dice Game";
    
    private readonly ChatMessageProcessor chatProcessor;
    private readonly RollHandler rollHandler;
    private readonly RollCollector rollCollector;
    private readonly GameSessionManager<DiceGameState> gameSession;
    private readonly DiceGameConfig config;
    
    public DiceGamePlugin(IChatGui chatGui, IClientState clientState, ICommandManager commands)
    {
        // Initialize components
        config = new DiceGameConfig();
        chatProcessor = new ChatMessageProcessor();
        rollHandler = new RollHandler(clientState.LocalPlayer?.Name.TextValue);
        rollCollector = new RollCollector();
        gameSession = new GameSessionManager<DiceGameState>();
        
        // Wire up events
        chatProcessor.RegisterHandler(rollHandler);
        rollHandler.RollDetected += OnRollDetected;
        gameSession.StateChanged += OnGameStateChanged;
        gameSession.SessionCompleted += OnGameCompleted;
        
        // Connect to Dalamud
        chatGui.ChatMessage += (type, time, sender, msg, handled) => 
            chatProcessor.ProcessMessage((int)type, time, sender.TextValue, msg.TextValue);
        
        commands.AddHandler("/dicegame", new CommandInfo(OnDiceCommand));
    }
    
    private void OnRollDetected(RollEventArgs roll)
    {
        if (!gameSession.IsActive) return;
        
        if (rollCollector.AddRoll(roll))
        {
            chatGui.Print($"[Dice] {roll.NormalizedPlayerName}: {roll.RollValue}");
            
            // Auto-end when enough players have rolled
            if (rollCollector.GetRollCount() >= config.MaxPlayers)
            {
                EndGame();
            }
        }
    }
    
    private void OnDiceCommand(string command, string args)
    {
        switch (args.ToLower())
        {
            case "start":
                StartGame();
                break;
            case "stop":
                gameSession.StopSession();
                break;
            case "status":
                ShowGameStatus();
                break;
        }
    }
    
    private void StartGame()
    {
        if (gameSession.StartSessionWithTimeout(TimeSpan.FromSeconds(config.RollTimeout)))
        {
            rollCollector.ClearRolls();
            chatGui.Print($"[Dice] Game started! Roll within {config.RollTimeout} seconds!");
        }
    }
    
    private void EndGame()
    {
        var winner = rollCollector.GetHighestRoll();
        if (winner.HasValue)
        {
            config.LastWinner = winner.Value.playerName;
            config.Save();
            gameSession.CompleteSession(winner.Value.playerName);
        }
    }
    
    public void Dispose()
    {
        gameSession.Dispose();
    }
}

public class DiceGameState
{
    public int RoundNumber { get; set; } = 1;
    public DateTime StartTime { get; set; } = DateTime.Now;
}

public class DiceGameConfig : JsonFileConfiguration
{
    public int RollTimeout { get; set; } = 30;
    public int MaxPlayers { get; set; } = 8;
    public string LastWinner { get; set; } = "";
    
    public DiceGameConfig() : base("config.json") { Load(); }
}
```

---

## 🔍 **Best Practices**

### **Performance**
- ✅ Reuse `ChatMessageProcessor` instances
- ✅ Unregister handlers in `Dispose()`
- ✅ Use `GameSessionManager` for state management instead of manual flags

### **Error Handling**
- ✅ Wrap configuration operations in try-catch
- ✅ Check `gameSession.IsActive` before processing events
- ✅ Validate player names before normalization

### **Thread Safety**
- ✅ All library components are thread-safe
- ✅ Use `UpdateGameState()` for safe state modifications
- ✅ Configuration save/load operations are atomic

---

## 📖 **Additional Resources**

- **[Migration Guide](VERSIONING_MIGRATION_GUIDE.md)** - Moving from custom versioning
- **[Troubleshooting](TROUBLESHOOTING.md)** - Common issues and solutions
- **[Quick Reference](QUICK_REFERENCE.md)** - Cheat sheet for common tasks

---

Happy coding! 🚀