using FFXIVSharedLibrary.Player;
using FFXIVSharedLibrary.Chat;
using FFXIVSharedLibrary.GameState;
using FFXIVSharedLibrary.Build;
using FFXIVSharedLibrary.Configuration;
using System.Text.RegularExpressions;

namespace TestConsole;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== FFXIVSharedLibrary Functionality Test ===\n");

        TestPlayerNameNormalizer();
        TestServerData();
        TestRollHandler();
        TestRollHandlerDebugMode();
        TestDebugConfiguration();
        TestGameSessionManager();
        TestVersioningHelper();

        Console.WriteLine("\n=== All Tests Complete ===");
    }

    static void TestPlayerNameNormalizer()
    {
        Console.WriteLine("🎭 Testing PlayerNameNormalizer...");
        
        var testCases = new[]
        {
            "Kirin Blackthorne",
            "Kirin Blackthorne Gilgamesh", 
            "Player Name Excalibur",
            "You",
            "Test Player Balmung"
        };

        var localPlayerName = "MyCharacter Name";

        foreach (var testName in testCases)
        {
            var normalized = PlayerNameNormalizer.NormalizeName(testName);
            var withLocal = PlayerNameNormalizer.NormalizeNameWithLocalPlayer(testName, localPlayerName);
            
            Console.WriteLine($"  '{testName}' → '{normalized}' (with local: '{withLocal}')");
        }

        Console.WriteLine("✅ PlayerNameNormalizer tests complete\n");
    }

    static void TestServerData()
    {
        Console.WriteLine("🌍 Testing ServerData...");
        
        var testServers = new[] { "Gilgamesh", "Balmung", "Tonberry", "InvalidServer", "Phoenix" };
        
        foreach (var server in testServers)
        {
            var isValid = ServerData.IsValidServer(server);
            var datacenter = ServerData.GetDatacenterForServer(server);
            var region = ServerData.GetRegionForServer(server);
            
            Console.WriteLine($"  {server}: Valid={isValid}, DC={datacenter}, Region={region}");
        }

        Console.WriteLine($"  Total servers: {ServerData.AllServers.Count}");
        Console.WriteLine($"  Aether servers: {ServerData.GetServersInDatacenter("Aether").Count}");
        
        Console.WriteLine("✅ ServerData tests complete\n");
    }

    static void TestRollHandler()
    {
        Console.WriteLine("🎲 Testing RollHandler...");
        
        var processor = new ChatMessageProcessor();
        var rollHandler = new RollHandler("MyCharacter Name");
        var rollCollector = new RollCollector();
        
        processor.RegisterHandler(rollHandler);
        
        rollHandler.RollDetected += (roll) => {
            rollCollector.AddRoll(roll);
            Console.WriteLine($"  Roll detected: {roll.NormalizedPlayerName} = {roll.RollValue}");
        };

        var testMessages = new[]
        {
            "Random! Kirin Blackthorne rolls a 42.",
            "Random! You roll a 78.",
            "Random! Player Name Gilgamesh rolls a 15.",
            "Not a roll message",
            "Random! Test Player Balmung rolls a 99.",
            "Random! Kirin Blackthorne rolls a 33." // Duplicate, should be ignored
        };

        foreach (var message in testMessages)
        {
            processor.ProcessMessage(0, 0, "TestSender", message);
        }

        var highest = rollCollector.GetHighestRoll();
        Console.WriteLine($"  Highest roll: {highest?.playerName} with {highest?.rollValue}");
        Console.WriteLine($"  Total unique rolls: {rollCollector.GetRollCount()}");
        
        Console.WriteLine("✅ RollHandler tests complete\n");
    }

    static void TestRollHandlerDebugMode()
    {
        Console.WriteLine("🎲🐛 Testing RollHandler Debug Mode...");
        
        var processor = new ChatMessageProcessor();
        var rollHandler = new RollHandler("MyCharacter Name", debugMode: true);
        var rollCollector = new RollCollector();
        
        processor.RegisterHandler(rollHandler);
        
        rollHandler.RollDetected += (roll) => {
            rollCollector.AddRoll(roll);
            var debugInfo = roll.IsDebugRoll ? $" [DEBUG: max {roll.MaxRollValue}]" : "";
            Console.WriteLine($"  Roll detected: {roll.NormalizedPlayerName} = {roll.RollValue}{debugInfo} (Order: {roll.RollOrder})");
        };

        Console.WriteLine("  Testing debug mode ENABLED...");
        
        var testMessages = new[]
        {
            "Random! Kirin Blackthorne rolls a 42.",                    // Normal roll
            "Random! You roll a 2 (out of 2).",                        // Debug roll
            "Random! Player Name Gilgamesh rolls a 1 (out of 2).",     // Debug roll  
            "Random! Test Player Balmung rolls a 99.",                  // Normal roll
            "Random! Another Player rolls a 2 (out of 5).",            // Debug roll
            "Not a roll message",                                       // Should be ignored
            "Random! Final Player rolls a 3 (out of 10)."              // Debug roll
        };

        foreach (var message in testMessages)
        {
            processor.ProcessMessage(0, 0, "TestSender", message);
        }

        Console.WriteLine($"  Total rolls: {rollCollector.GetRollCount()}");
        Console.WriteLine($"  Debug rolls: {rollCollector.GetDebugRollCount()}");
        
        var highest = rollCollector.GetHighestRoll();
        Console.WriteLine($"  Highest roll: {highest?.playerName} with {highest?.rollValue}");
        
        var tiedRolls = rollCollector.GetTiedRolls();
        if (tiedRolls.Count > 1)
        {
            Console.WriteLine($"  Tied players: {string.Join(", ", tiedRolls.Select(r => $"{r.NormalizedPlayerName} ({r.RollOrder})"))}");
        }
        
        var winner = rollCollector.GetWinnerWithTiebreaker();
        Console.WriteLine($"  Winner (with tiebreaker): {winner?.NormalizedPlayerName}");

        // Test debug mode toggle
        Console.WriteLine("  Testing debug mode DISABLED...");
        rollHandler.SetDebugMode(false);
        rollCollector.ClearRolls();
        
        var debugOnlyMessages = new[]
        {
            "Random! You roll a 1 (out of 2).",                        // Should be ignored
            "Random! Player rolls a 50.",                              // Should be detected
            "Random! Another Player rolls a 3 (out of 5)."             // Should be ignored
        };
        
        foreach (var message in debugOnlyMessages)
        {
            processor.ProcessMessage(0, 0, "TestSender", message);
        }
        
        Console.WriteLine($"  Rolls with debug disabled: {rollCollector.GetRollCount()} (should be 1)");
        
        Console.WriteLine("✅ RollHandler debug mode tests complete\n");
    }

    static void TestDebugConfiguration()
    {
        Console.WriteLine("⚙️🐛 Testing Debug Configuration...");
        
        var tempPath = Path.Combine(Path.GetTempPath(), "TestDebugConfig.json");
        
        try
        {
            var config = new TestDebugConfig(tempPath);
            
            Console.WriteLine($"  Initial debug mode: {config.DebugMode}");
            Console.WriteLine($"  Initial roll timeout: {config.RollTimeout}");
            
            // Test setting debug mode
            config.DebugMode = true;
            config.RollTimeout = 45;
            config.Save();
            
            Console.WriteLine($"  After changes - Debug mode: {config.DebugMode}, Timeout: {config.RollTimeout}");
            
            // Test loading from file
            var loadedConfig = new TestDebugConfig(tempPath);
            loadedConfig.Load();
            
            Console.WriteLine($"  Loaded from file - Debug mode: {loadedConfig.DebugMode}, Timeout: {loadedConfig.RollTimeout}");
            
            // Test callback functionality
            Console.WriteLine("  Testing debug mode change callback...");
            loadedConfig.DebugMode = false; // Should trigger callback
            
            Console.WriteLine("✅ Debug configuration tests complete\n");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    static void TestGameSessionManager()
    {
        Console.WriteLine("🎮 Testing GameSessionManager...");
        
        var gameSession = new GameSessionManager<TestGameState>();
        
        gameSession.StateChanged += (oldState, newState, state) => {
            Console.WriteLine($"  State changed: {oldState} → {newState}");
        };
        
        gameSession.SessionCompleted += (state, winner) => {
            Console.WriteLine($"  Session completed! Winner: {winner ?? "None"}");
        };

        Console.WriteLine($"  Initial state: {gameSession.CurrentState}");
        
        // Start session
        gameSession.StartSession(new TestGameState { PlayerCount = 0 });
        
        // Update state
        gameSession.UpdateGameState(state => {
            state.PlayerCount = 5;
            state.IsRollingPhase = true;
        });
        
        var playerCount = gameSession.GetGameStateValue(state => state.PlayerCount);
        Console.WriteLine($"  Player count: {playerCount}");
        
        // Complete session
        gameSession.CompleteSession("TestWinner");
        
        Console.WriteLine("✅ GameSessionManager tests complete\n");
    }

    static void TestVersioningHelper()
    {
        Console.WriteLine("🔧 Testing VersioningHelper...");
        
        // Create a test JSON file
        var testJsonPath = Path.Combine(Path.GetTempPath(), "TestPlugin.json");
        var testJson = """
        {
          "Author": "TestAuthor",
          "Name": "Test Plugin",
          "InternalName": "TestPlugin",
          "AssemblyVersion": "1.0.0.0",
          "Description": "A test plugin",
          "DalamudApiLevel": 12
        }
        """;
        
        File.WriteAllText(testJsonPath, testJson);
        
        try
        {
            // Test getting version
            var originalVersion = VersioningHelper.GetVersionFromJson(testJsonPath);
            Console.WriteLine($"  Original version: {originalVersion}");
            
            // Test updating version
            VersioningHelper.UpdateJsonVersion(testJsonPath, "2.5.1.0");
            var updatedVersion = VersioningHelper.GetVersionFromJson(testJsonPath);
            Console.WriteLine($"  Updated version: {updatedVersion}");
            
            // Test multiple file handling
            var testFiles = new[] { testJsonPath };
            var versions = VersioningHelper.GetVersionsFromMultipleJson(testFiles);
            Console.WriteLine($"  Multi-file test: {versions.Count} files processed");
            
            Console.WriteLine("✅ VersioningHelper tests complete\n");
        }
        finally
        {
            if (File.Exists(testJsonPath))
                File.Delete(testJsonPath);
        }
    }
}

public class TestGameState
{
    public int PlayerCount { get; set; }
    public bool IsRollingPhase { get; set; }
    public Dictionary<string, int> Rolls { get; set; } = new();
}

public class TestDebugConfig : JsonFileDebugConfiguration
{
    public int RollTimeout { get; set; } = 30;
    public string PlayerName { get; set; } = "Default Player";

    public TestDebugConfig(string filePath) : base(filePath)
    {
    }

    protected override void OnDebugModeChanged(bool enabled)
    {
        Console.WriteLine($"    → Debug mode changed to: {enabled}");
        if (enabled)
        {
            Console.WriteLine("    → Debug roll detection is now active");
        }
        else
        {
            Console.WriteLine("    → Debug roll detection is now disabled");
        }
    }
}