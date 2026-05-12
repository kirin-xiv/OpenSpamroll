# ⚡ FFXIVSharedLibrary Quick Reference

> **Cheat sheet** for common tasks and code snippets. Perfect for quick copy-paste!

---

## 🔧 **Setup (Copy-Paste Ready)**

### **Add to .csproj:**
```xml
<ItemGroup>
    <ProjectReference Include="..\..\FFXIVToolkit\FFXIVSharedLibrary\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
</ItemGroup>
```

### **Using Statements:**
```csharp
using FFXIVSharedLibrary.Player;
using FFXIVSharedLibrary.Chat;
using FFXIVSharedLibrary.GameState;
using FFXIVSharedLibrary.Configuration;
using FFXIVSharedLibrary.Build;
```

---

## 🏗️ **Automatic Versioning**

### **Basic Setup:**
```xml
<PropertyGroup>
    <Version>1.5.0.0</Version>  <!-- Change this to update everywhere -->
    <AssemblyVersion>$(Version)</AssemblyVersion>
    <FileVersion>$(Version)</FileVersion>
</PropertyGroup>
```

### **Programmatic Update:**
```csharp
VersioningHelper.UpdateJsonVersion("Plugin.json", "2.1.0.0");
```

---

## 🎭 **Player Names**

```csharp
// Remove server names
var clean = PlayerNameNormalizer.NormalizeName("Player Name Gilgamesh");
// Result: "Player Name"

// Handle "You" case
var player = PlayerNameNormalizer.NormalizeNameWithLocalPlayer("You", localPlayerName);

// Check server validity
bool valid = ServerData.IsValidServer("Gilgamesh"); // true

// Get server info
string dc = ServerData.GetDatacenterForServer("Gilgamesh");  // "Aether"
string region = ServerData.GetRegionForServer("Gilgamesh");  // "North America"
```

---

## 💬 **Chat & Rolls**

### **Basic Setup:**
```csharp
var chatProcessor = new ChatMessageProcessor();
var rollHandler = new RollHandler(localPlayerName);
var rollCollector = new RollCollector();

chatProcessor.RegisterHandler(rollHandler);
rollHandler.RollDetected += OnRollDetected;

// Connect to Dalamud
chatGui.ChatMessage += (type, time, sender, msg, handled) => 
    chatProcessor.ProcessMessage((int)type, time, sender.TextValue, msg.TextValue);
```

### **Handle Rolls:**
```csharp
private void OnRollDetected(RollEventArgs roll)
{
    if (rollCollector.AddRoll(roll)) // false if duplicate
    {
        chatGui.Print($"{roll.NormalizedPlayerName} rolled {roll.RollValue}!");
    }
}

// Get winner
var winner = rollCollector.GetHighestRoll();
if (winner.HasValue)
    chatGui.Print($"Winner: {winner.Value.playerName} ({winner.Value.rollValue})");

// Clear for next round
rollCollector.ClearRolls();
```

### **Custom Chat Handler:**
```csharp
public class MyHandler : RegexChatHandler
{
    public override int Priority => 100;
    public MyHandler() : base(@"!command (.+)", RegexOptions.IgnoreCase) { }
    
    public override void Handle(ChatMessageEventArgs args)
    {
        var match = GetMatch(args);
        var param = match.Groups[1].Value;
        // Do something with param
        args.IsHandled = true; // Stop further processing
    }
}

chatProcessor.RegisterHandler(new MyHandler());
```

---

## 🎮 **Game Sessions**

### **Basic Game State:**
```csharp
public class MyGameState
{
    public Dictionary<string, int> Rolls { get; set; } = new();
    public string Winner { get; set; } = "";
    public int Round { get; set; } = 1;
}

var gameSession = new GameSessionManager<MyGameState>();

// Events
gameSession.StateChanged += (old, new, state) => 
    chatGui.Print($"Game: {old} → {new}");
gameSession.SessionCompleted += (state, winner) => 
    chatGui.Print($"Winner: {winner}");
```

### **Session Control:**
```csharp
// Start session
if (gameSession.StartSession(new MyGameState()))
    chatGui.Print("Game started!");

// Start with timeout
gameSession.StartSessionWithTimeout(TimeSpan.FromSeconds(30));

// Update state safely
gameSession.UpdateGameState(state => {
    state.Rolls["PlayerName"] = 42;
    state.Round++;
});

// Get value from state
var round = gameSession.GetGameStateValue(state => state.Round);

// End session
gameSession.CompleteSession("WinnerName");
gameSession.StopSession(); // Without winner
```

### **Timed Sessions:**
```csharp
var timedSession = new TimedGameSessionManager<MyGameState>(TimeSpan.FromMinutes(5));
timedSession.StartSession(new MyGameState()); // Auto-stops after 5 min
```

---

## ⚙️ **Configuration**

### **JSON Configuration:**
```csharp
public class MyConfig : JsonFileConfiguration
{
    public int Timeout { get; set; } = 30;
    public string LastWinner { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<string> Players { get; set; } = new();
    
    public MyConfig() : base("config.json") { Load(); }
}

var config = new MyConfig();
config.Timeout = 45;
config.Save(); // Saves to JSON file
```

### **Memory Configuration:**
```csharp
public class TempConfig : MemoryConfiguration
{
    public bool DebugMode { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

var temp = new TempConfig();
temp.DebugMode = true;
temp.Save(); // Saves to memory only
```

### **Dynamic Access:**
```csharp
config.SetValue("Timeout", 60);
var timeout = config.GetValue<int>("Timeout", 30); // 30 = default
var allSettings = config.GetAllValues();
```

---

## 📝 **Command Patterns**

### **Basic Plugin Structure:**
```csharp
public class MyPlugin : IDalamudPlugin
{
    public string Name => "My Plugin";
    
    private readonly ChatMessageProcessor chatProcessor;
    private readonly RollHandler rollHandler;
    private readonly GameSessionManager<MyGameState> gameSession;
    private readonly MyConfig config;
    
    public MyPlugin(IChatGui chatGui, IClientState clientState, ICommandManager commands)
    {
        config = new MyConfig();
        chatProcessor = new ChatMessageProcessor();
        rollHandler = new RollHandler(clientState.LocalPlayer?.Name.TextValue);
        gameSession = new GameSessionManager<MyGameState>();
        
        SetupEvents();
        commands.AddHandler("/myplugin", new CommandInfo(OnCommand));
        chatGui.ChatMessage += OnChatMessage;
    }
    
    private void SetupEvents()
    {
        chatProcessor.RegisterHandler(rollHandler);
        rollHandler.RollDetected += OnRollDetected;
        gameSession.StateChanged += OnStateChanged;
    }
    
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        chatProcessor.ProcessMessage((int)type, timestamp, sender.TextValue, message.TextValue);
    }
    
    private void OnCommand(string command, string args)
    {
        switch (args.ToLower())
        {
            case "start": StartGame(); break;
            case "stop": gameSession.StopSession(); break;
            case "status": ShowStatus(); break;
        }
    }
    
    public void Dispose()
    {
        gameSession.Dispose();
    }
}
```

---

## 🎯 **Common Patterns**

### **Roll-Based Mini Game:**
```csharp
[Command("/rollgame")]
private void RollGameCommand(string command, string args)
{
    if (args == "start")
    {
        if (gameSession.StartSessionWithTimeout(TimeSpan.FromSeconds(30)))
        {
            rollCollector.ClearRolls();
            chatGui.Print("[Game] 30 seconds to roll!");
        }
    }
}

private void OnRollDetected(RollEventArgs roll)
{
    if (gameSession.IsActive && rollCollector.AddRoll(roll))
    {
        chatGui.Print($"[Game] {roll.NormalizedPlayerName}: {roll.RollValue}");
        
        if (rollCollector.GetRollCount() >= config.MaxPlayers)
            EndGame();
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
```

### **Player List Management:**
```csharp
private readonly HashSet<string> participants = new();

private void OnRollDetected(RollEventArgs roll)
{
    var playerName = roll.NormalizedPlayerName;
    
    if (participants.Add(playerName)) // Returns false if already exists
    {
        chatGui.Print($"[Game] {playerName} joined! ({participants.Count} players)");
    }
}
```

### **Server-Specific Logic:**
```csharp
private void OnRollDetected(RollEventArgs roll)
{
    var datacenter = ServerData.GetDatacenterForServer(GetPlayerServer(roll.PlayerName));
    var region = ServerData.GetRegionForServer(GetPlayerServer(roll.PlayerName));
    
    if (region == "North America")
    {
        // NA-specific logic
    }
}
```

---

## 🔍 **Debug Helpers**

### **State Inspection:**
```csharp
// Check session state
Console.WriteLine($"Active: {gameSession.IsActive}");
Console.WriteLine($"State: {gameSession.CurrentState}");

// Check roll collector
Console.WriteLine($"Rolls: {rollCollector.GetRollCount()}");
var allRolls = rollCollector.GetAllRolls();
foreach (var (player, roll) in allRolls)
    Console.WriteLine($"  {player}: {roll.RollValue}");

// Check server data
Console.WriteLine($"Total servers: {ServerData.AllServers.Count}");
Console.WriteLine($"Is Gilgamesh valid: {ServerData.IsValidServer("Gilgamesh")}");
```

### **Configuration Debug:**
```csharp
var allSettings = config.GetAllValues();
foreach (var (key, value) in allSettings)
    Console.WriteLine($"{key}: {value}");
```

---

## 🚨 **Error Handling**

### **Safe Configuration:**
```csharp
try
{
    config.Save();
}
catch (Exception ex)
{
    chatGui.PrintError($"[Plugin] Failed to save config: {ex.Message}");
}
```

### **Safe State Updates:**
```csharp
if (gameSession.IsActive)
{
    gameSession.UpdateGameState(state => {
        // Safe to modify state here
        state.Round++;
    });
}
```

### **Validation:**
```csharp
if (string.IsNullOrWhiteSpace(playerName)) return;
if (!ServerData.IsValidServer(serverName)) return;
if (!gameSession.IsActive) return;
```

---

## 📚 **Full Documentation**

- **[Migration Guide](VERSIONING_MIGRATION_GUIDE.md)** - Move from old versioning
- **[Usage Guide](USAGE_GUIDE.md)** - Complete documentation  
- **[Troubleshooting](TROUBLESHOOTING.md)** - Fix common issues

---

Happy coding! 🚀