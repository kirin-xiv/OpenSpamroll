# Debug Mode Integration Guide

This guide shows how to integrate the FFXIVSharedLibrary debug mode functionality into your FFXIV plugins for enhanced roll detection and tiebreaker testing.

## Overview

Debug mode allows plugins to detect both normal FFXIV rolls and debug patterns from `/random X` commands, making tiebreaker testing much easier during development and QA.

### Supported Patterns

- **Normal rolls**: `Random! Player Name rolls a 123.` or `Random! You roll a 456.`
- **Debug rolls**: `Random! Player Name rolls a 2 (out of 2).` or `Random! You roll a 1 (out of 5).`

## Quick Integration Example

```csharp
using FFXIVSharedLibrary.Chat;
using FFXIVSharedLibrary.Configuration;

public class MyPluginConfig : JsonFileDebugConfiguration
{
    public int RollTimeout { get; set; } = 30;
    public string LastWinner { get; set; } = "";
    // DebugMode inherited automatically from JsonFileDebugConfiguration

    public MyPluginConfig() : base("MyPlugin.json") { }
    
    protected override void OnDebugModeChanged(bool enabled)
    {
        chatGui.Print($"Debug mode {(enabled ? "enabled" : "disabled")}");
        rollHandler.SetDebugMode(enabled);
    }
}

public class MyPlugin : IDalamudPlugin
{
    private RollHandler rollHandler;
    private RollCollector rollCollector;
    private MyPluginConfig config;
    
    public void Initialize()
    {
        config = new MyPluginConfig();
        config.Load();
        
        rollHandler = new RollHandler(clientState.LocalPlayer?.Name.TextValue, config.DebugMode);
        rollCollector = new RollCollector();
        
        rollHandler.RollDetected += OnRollDetected;
        chatGui.ChatMessage += (_, args) => {
            chatProcessor.ProcessMessage(args.Type, 0, args.Sender?.TextValue ?? "", args.Message.TextValue);
        };
    }
    
    private void OnRollDetected(RollEventArgs roll)
    {
        if (rollCollector.AddRoll(roll))
        {
            if (roll.IsDebugRoll)
            {
                chatGui.Print($"🐛 {roll.NormalizedPlayerName}: {roll.RollValue} [DEBUG: max {roll.MaxRollValue}]");
            }
            else
            {
                chatGui.Print($"🎲 {roll.NormalizedPlayerName}: {roll.RollValue}");
            }
        }
    }
}
```

## Configuration Setup

### 1. Basic Configuration Class

Create a configuration class that extends `JsonFileDebugConfiguration`:

```csharp
public class MyRollPluginConfig : JsonFileDebugConfiguration
{
    public int RollTimeout { get; set; } = 30;
    public bool ShowRollOrder { get; set; } = true;
    public string RollCommand { get; set; } = "/roll";
    
    public MyRollPluginConfig() : base("MyRollPlugin.json") { }
    
    protected override void OnDebugModeChanged(bool enabled)
    {
        // This callback is triggered whenever DebugMode changes
        PluginLog.Information($"Debug mode changed to: {enabled}");
        
        // Update your roll handler
        rollHandler?.SetDebugMode(enabled);
        
        // Show UI notification
        if (enabled)
        {
            chatGui.Print("🐛 Debug mode enabled - /random X patterns will be detected");
        }
        else
        {
            chatGui.Print("📊 Debug mode disabled - only normal rolls detected");
        }
    }
}
```

### 2. Initialize Configuration

```csharp
private MyRollPluginConfig config;

public void Initialize()
{
    config = new MyRollPluginConfig();
    config.Load(); // Load from file if it exists
    
    // Create roll handler with debug mode from config
    rollHandler = new RollHandler(
        clientState.LocalPlayer?.Name.TextValue, 
        config.DebugMode
    );
}
```

## Roll Handler Setup

### 1. Enhanced Roll Detection

```csharp
private void SetupRollHandler()
{
    rollHandler = new RollHandler(localPlayerName, config.DebugMode);
    rollCollector = new RollCollector();
    
    rollHandler.RollDetected += (roll) => {
        // Add to collector (returns false if duplicate)
        if (rollCollector.AddRoll(roll))
        {
            // Display roll with debug information
            var debugInfo = roll.IsDebugRoll ? $" [max: {roll.MaxRollValue}]" : "";
            var orderInfo = config.ShowRollOrder ? $" (#{roll.RollOrder})" : "";
            
            chatGui.Print($"🎲 {roll.NormalizedPlayerName}: {roll.RollValue}{debugInfo}{orderInfo}");
            
            // Check for winner with automatic tiebreaker
            var winner = rollCollector.GetWinnerWithTiebreaker();
            if (winner != null)
            {
                var tiedRolls = rollCollector.GetTiedRolls();
                if (tiedRolls.Count > 1)
                {
                    chatGui.Print($"🏆 {winner.NormalizedPlayerName} wins! (First of {tiedRolls.Count} tied players)");
                }
                else
                {
                    chatGui.Print($"🏆 {winner.NormalizedPlayerName} wins with {winner.RollValue}!");
                }
            }
        }
    };
}
```

### 2. Runtime Debug Mode Toggle

```csharp
[Command("/debugroll")]
[HelpMessage("Toggle debug mode for roll detection")]
public void ToggleDebugMode(string command, string args)
{
    config.DebugMode = !config.DebugMode;
    config.Save();
    
    // OnDebugModeChanged callback will be triggered automatically
}

[Command("/testroll")]
[HelpMessage("Simulate debug rolls for testing")]
public void TestRolls(string command, string args)
{
    if (!config.DebugMode)
    {
        chatGui.PrintError("Debug mode must be enabled to use test rolls");
        return;
    }
    
    var testMessages = new[]
    {
        "Random! You roll a 1 (out of 2).",
        "Random! Test Player rolls a 1 (out of 2).",
        "Random! Another Player rolls a 2 (out of 2)."
    };
    
    foreach (var message in testMessages)
    {
        chatProcessor.ProcessMessage(0, 0, "TestSender", message);
    }
    
    chatGui.Print("Test rolls sent - check for tiebreaker resolution!");
}
```

## UI Integration

### 1. Configuration Window (ImGui)

```csharp
private void DrawConfigWindow()
{
    if (ImGui.Begin("My Roll Plugin Config", ref configWindowOpen))
    {
        // Debug mode toggle
        var debugMode = config.DebugMode;
        if (ImGui.Checkbox("Debug Mode (detect /random X patterns)", ref debugMode))
        {
            config.DebugMode = debugMode;
            config.Save();
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("When enabled, detects both normal rolls and /random X patterns for easier tiebreaker testing");
        }
        
        // Show debug indicator
        if (config.DebugMode)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "🐛 DEBUG ACTIVE");
        }
        
        // Roll timeout setting
        var timeout = config.RollTimeout;
        if (ImGui.SliderInt("Roll Timeout (seconds)", ref timeout, 10, 120))
        {
            config.RollTimeout = timeout;
            config.Save();
        }
        
        // Statistics display
        ImGui.Separator();
        ImGui.Text("Current Session:");
        ImGui.Text($"Total Rolls: {rollCollector.GetRollCount()}");
        ImGui.Text($"Debug Rolls: {rollCollector.GetDebugRollCount()}");
        
        var winner = rollCollector.GetWinnerWithTiebreaker();
        if (winner != null)
        {
            ImGui.Text($"Winner: {winner.NormalizedPlayerName} ({winner.RollValue})");
        }
        
        if (ImGui.Button("Clear Rolls"))
        {
            rollCollector.ClearRolls();
        }
    }
    ImGui.End();
}
```

### 2. Main Plugin UI

```csharp
private void DrawMainWindow()
{
    if (ImGui.Begin("Roll Tracker", ref mainWindowOpen))
    {
        // Debug mode indicator
        if (config.DebugMode)
        {
            ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "🐛 DEBUG MODE");
            ImGui.SameLine();
            if (ImGui.Button("Disable##debug"))
            {
                config.DebugMode = false;
                config.Save();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "📊 NORMAL MODE");
            ImGui.SameLine();
            if (ImGui.Button("Enable Debug##debug"))
            {
                config.DebugMode = true;
                config.Save();
            }
        }
        
        ImGui.Separator();
        
        // Display current rolls
        var allRolls = rollCollector.GetAllRolls();
        if (allRolls.Any())
        {
            ImGui.Text("Current Rolls:");
            
            foreach (var roll in allRolls.Values.OrderByDescending(r => r.RollValue).ThenBy(r => r.RollOrder))
            {
                var debugIcon = roll.IsDebugRoll ? "🐛" : "🎲";
                var maxInfo = roll.IsDebugRoll ? $"/{roll.MaxRollValue}" : "";
                
                ImGui.Text($"{debugIcon} {roll.NormalizedPlayerName}: {roll.RollValue}{maxInfo}");
            }
            
            // Show tiebreaker information
            var tiedRolls = rollCollector.GetTiedRolls();
            if (tiedRolls.Count > 1)
            {
                ImGui.Separator();
                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Tiebreaker Resolution:");
                ImGui.Text($"{tiedRolls[0].NormalizedPlayerName} wins (first to roll)");
            }
        }
        else
        {
            ImGui.Text("No rolls yet...");
            
            if (config.DebugMode)
            {
                ImGui.TextDisabled("Tip: Use /random 2 to test tiebreakers");
            }
        }
    }
    ImGui.End();
}
```

## Advanced Usage

### 1. Custom Tiebreaker Logic

```csharp
public class CustomRollCollector : RollCollector
{
    public RollEventArgs? GetWinnerWithCustomTiebreaker()
    {
        var tiedRolls = GetTiedRolls();
        if (tiedRolls.Count <= 1) return tiedRolls.FirstOrDefault();
        
        // Custom logic: In debug mode, highest max value wins ties
        // In normal mode, first roller wins
        if (tiedRolls.Any(r => r.IsDebugRoll))
        {
            return tiedRolls
                .Where(r => r.IsDebugRoll)
                .OrderByDescending(r => r.MaxRollValue ?? 0)
                .ThenBy(r => r.RollOrder)
                .FirstOrDefault();
        }
        
        return tiedRolls.OrderBy(r => r.RollOrder).FirstOrDefault();
    }
}
```

### 2. Event Handling for Statistics

```csharp
private void SetupEventHandlers()
{
    rollCollector.NewRollAdded += (roll) => {
        // Log roll statistics
        PluginLog.Information($"Roll added: {roll.NormalizedPlayerName}={roll.RollValue} (Debug: {roll.IsDebugRoll})");
        
        // Update UI counters
        UpdateRollCounters();
    };
    
    rollCollector.RollsCleared += (clearedRolls) => {
        PluginLog.Information($"Cleared {clearedRolls.Count} rolls");
        var debugCount = clearedRolls.Values.Count(r => r.IsDebugRoll);
        chatGui.Print($"Session ended: {clearedRolls.Count} total rolls ({debugCount} debug)");
    };
}
```

## Testing Scenarios

### 1. Debug Mode Testing

```bash
# Enable debug mode
/debugroll

# Test tiebreaker scenarios
/random 2  # You: 1 (out of 2)
/tell Friend "try /random 2"  # Friend: 1 (out of 2)
# Result: First roller wins automatically

# Test mixed scenarios  
/random    # You: 847 (normal)
/tell Friend "try /random 2"  # Friend: 1 (out of 2)
# Both detected when debug mode is on
```

### 2. Performance Testing

```csharp
[Command("/rollstress")]
public void StressTestRolls(string command, string args)
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    for (int i = 0; i < 1000; i++)
    {
        var message = $"Random! Player{i} rolls a {Random.Next(1, 1000)}.";
        chatProcessor.ProcessMessage(0, 0, "TestSender", message);
    }
    
    stopwatch.Stop();
    chatGui.Print($"Processed 1000 rolls in {stopwatch.ElapsedMilliseconds}ms");
}
```

## Best Practices

1. **Always use debug mode for testing** - Makes tiebreaker scenarios predictable
2. **Provide clear UI indicators** - Users should know when debug mode is active
3. **Save configuration changes immediately** - Ensures debug mode persists across restarts
4. **Handle both roll types gracefully** - Display appropriate information for each type
5. **Use automatic tiebreakers** - Keeps games moving without manual intervention
6. **Log debug information** - Helps with troubleshooting and analytics

## Migration from Existing Plugins

If you have an existing plugin using the old RollHandler, update as follows:

```csharp
// Old way
rollHandler = new RollHandler(localPlayerName);

// New way with debug mode support
rollHandler = new RollHandler(localPlayerName, config.DebugMode);

// Update configuration class
public class MyConfig : JsonFileDebugConfiguration  // instead of JsonFileConfiguration
{
    // Your existing properties
    // DebugMode is inherited automatically
}
```

The RollEventArgs now includes additional properties:
- `IsDebugRoll`: Whether this came from a /random X command
- `MaxRollValue`: The X value from /random X (null for normal rolls)
- `RollOrder`: Incremental counter for automatic tiebreaking

All existing functionality remains compatible - the new properties simply provide additional information when needed.