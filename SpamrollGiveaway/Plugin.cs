using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SpamrollGiveaway.Windows;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ECommons;
using ECommons.Automation;
using ECommons.Throttlers;

namespace SpamrollGiveaway;

public class Winner
{
    public string PlayerName { get; set; } = "";
    public int RollValue { get; set; }
    public DateTime WinTime { get; set; }
    public int RollOrder { get; set; }
    public bool IsDebugRoll { get; set; }
}

public class RollData
{
    public string PlayerName { get; set; } = "";
    public int RollValue { get; set; }
    public DateTime RollTime { get; set; }
    public bool IsDebugRoll { get; set; }
}

public sealed class Plugin : IDalamudPlugin
{
    // Windows API for playing system sounds
    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/spamroll";
    private const string CommandStartName = "/spamstart";
    private const string CommandStopName = "/spamstop";
    private const string CommandConfigName = "/spamconfig";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SpamrollGiveaway");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    // Game state
    private bool isGameActive = false;
    private bool isGamePaused = false;
    private readonly List<Winner> gameWinners = new();
    private int rollCounter = 0;
    private CancellationTokenSource? gameCancellation;
    private readonly object lockObject = new object();
    private DateTime gameStartTime;
    private DateTime? pendingInstructionTime;
    private readonly Queue<string> messageQueue = new();
    private DateTime lastMessageSent = DateTime.MinValue;


    public Plugin()
    {
        // Initialize ECommons
        ECommonsMain.Init(PluginInterface, this);
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Spamroll Giveaway main window"
        });

        CommandManager.AddHandler(CommandStartName, new CommandInfo(OnStartCommand)
        {
            HelpMessage = "Starts collecting rolls for Spamroll Giveaway"
        });

        CommandManager.AddHandler(CommandStopName, new CommandInfo(OnStopCommand)
        {
            HelpMessage = "Stops the current Spamroll Giveaway round"
        });

        CommandManager.AddHandler(CommandConfigName, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Opens the Spamroll Giveaway configuration window"
        });

        ChatGui.ChatMessage += OnChatMessage;
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
        // Subscribe to framework update for delayed message sending
        PluginInterface.UiBuilder.Draw += OnFrameworkUpdate;

        Log.Information($"Spamroll Giveaway loaded successfully!");
    }

    public void Dispose()
    {
        gameCancellation?.Cancel();
        gameCancellation?.Dispose();
        
        // Clean up any remaining resources

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandStartName);
        CommandManager.RemoveHandler(CommandStopName);
        CommandManager.RemoveHandler(CommandConfigName);

        ChatGui.ChatMessage -= OnChatMessage;
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
        PluginInterface.UiBuilder.Draw -= OnFrameworkUpdate;
        
        // Dispose ECommons
        ECommonsMain.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        if (args == "config")
            ToggleConfigUI();
        else
            ToggleMainUI();
    }

    private void OnStartCommand(string command, string args) => StartGame();
    private void OnStopCommand(string command, string args) => StopGame();
    private void OnConfigCommand(string command, string args) => ToggleConfigUI();

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!isGameActive || isGamePaused) return;

        var messageText = message.Message.TextValue;
        
        // Debug logging for roll detection
        if (messageText.Contains("Random!"))
        {
            Log.Information($"[Debug] Detected Random message: '{messageText}'");
        }
        
        // Detect roll patterns (normal and debug)
        Match rollMatch;
        string playerName;
        int rollValue;
        bool isDebugRoll = false;

        if (Configuration.DebugMode)
        {
            // Try debug pattern first
            rollMatch = Regex.Match(messageText, @"Random! (.+) rolls? a (\d+) \(out of \d+\)\.");
            if (rollMatch.Success)
            {
                isDebugRoll = true;
            }
            else
            {
                // Fall back to normal pattern
                rollMatch = Regex.Match(messageText, @"Random! (.+) rolls? a (\d+)\.");
            }
        }
        else
        {
            // Normal pattern only
            rollMatch = Regex.Match(messageText, @"Random! (.+) rolls? a (\d+)\.");
        }

        if (!rollMatch.Success) 
        {
            if (messageText.Contains("Random!"))
            {
                Log.Warning($"[Debug] Random message failed regex match: '{messageText}'");
            }
            return;
        }

        playerName = rollMatch.Groups[1].Value;
        rollValue = int.Parse(rollMatch.Groups[2].Value);
        
        Log.Information($"[Debug] Roll detected - Player: '{playerName}', Value: {rollValue}");

        // Extract and format player name with server
        var normalizedName = ExtractPlayerNameWithServer(playerName);
        

        // CRITICAL: Only process if this is a WINNING number
        var activeWinningNumbers = Configuration.WinningNumbers.Take(Configuration.WinningNumberCount);
        Log.Information($"[Debug] Checking if {rollValue} is in winning numbers: [{string.Join(", ", activeWinningNumbers)}]");
        
        if (!activeWinningNumbers.Contains(rollValue))
        {
            Log.Information($"[Debug] Roll {rollValue} is not a winning number, ignoring");
            return; // Ignore non-winning rolls completely
        }
        
        Log.Information($"[Debug] Roll {rollValue} IS a winning number!");

        lock (lockObject)
        {
            bool canWin = false;
            
            if (Configuration.AllowMultipleWinners)
            {
                // Multiple winners mode: Check if this specific number already has a winner
                canWin = !gameWinners.Any(w => w.RollValue == rollValue);
                
                // Check if this player already won a different number (only if not allowing same player multiple wins)
                if (canWin && !Configuration.AllowSamePlayerMultipleWins && gameWinners.Any(w => w.PlayerName == normalizedName))
                {
                    Log.Information($"[Debug] Player {normalizedName} already won with a different number, ignoring");
                    return;
                }
            }
            else
            {
                // Single winner mode: Check if anyone has won yet
                canWin = gameWinners.Count == 0;
            }

            if (!canWin)
            {
                if (Configuration.AllowMultipleWinners)
                {
                    Log.Information($"[Debug] Number {rollValue} already has a winner, ignoring");
                }
                else
                {
                    Log.Information($"[Debug] Game already has a winner, ignoring");
                }
                return;
            }

            // This is a winner! Record it
            var winner = new Winner
            {
                PlayerName = normalizedName,
                RollValue = rollValue,
                WinTime = DateTime.Now,
                RollOrder = rollCounter++,
                IsDebugRoll = isDebugRoll
            };

            gameWinners.Add(winner);

            // Play winner sound
            if (Configuration.EnableWinnerSound)
            {
                PlayWinnerSound(Configuration.SelectedSoundEffect);
            }

            // Announce winner (only if not in manual mode)
            var debugInfo = isDebugRoll ? " [DEBUG]" : "";
            if (!Configuration.ManualMode)
            {
                if (Configuration.UseCustomTemplates)
                {
                    var announcement = Configuration.WinnerAnnouncementTemplate
                        .Replace("{player}", normalizedName)
                        .Replace("{roll}", rollValue.ToString());
                    SendChatMessage($"{announcement}{debugInfo}");
                }
                else
                {
                    SendChatMessage($"WINNER: {normalizedName} rolled {rollValue}!{debugInfo}");
                }
            }

            // Auto-close logic
            if (Configuration.AutoCloseAfterFirstWinner && !Configuration.AllowMultipleWinners && gameWinners.Count == 1)
            {
                EndGame();
            }
            else if (Configuration.AllowMultipleWinners)
            {
                // Check if all winning numbers have been claimed
                var allWinningNumbers = Configuration.WinningNumbers.Take(Configuration.WinningNumberCount);
                var claimedNumbers = gameWinners.Select(w => w.RollValue).ToHashSet();
                if (allWinningNumbers.All(num => claimedNumbers.Contains(num)))
                {
                    if (!Configuration.ManualMode)
                    {
                        SendChatMessage("[Spamroll] All winning numbers have been claimed! Game complete.");
                    }
                    EndGame();
                }
            }
        }
    }

    private string ExtractPlayerNameWithServer(string playerName)
    {
        // Handle "You" case first
        if (playerName.Trim() == "You" && !string.IsNullOrEmpty(Configuration.LocalPlayerName))
        {
            return Configuration.LocalPlayerName;
        }

        // Just return the player name as-is
        // Server extraction removed as it requires external dependency
        return playerName.Trim();
    }

    private void PlayWinnerSound(int soundEffect)
    {
        try
        {
            if (Configuration.SoundType == SoundEffectType.GameSoundEffect)
            {
                // Play FFXIV in-game sound effect using ECommons Chat.SendMessage
                try 
                {
                    var command = $"/echo <se.{soundEffect}>";
                    Chat.SendMessage(command);
                    Log.Information($"Playing FFXIV in-game sound effect: {command}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to send sound effect command: {ex.Message}");
                }
            }
            else
            {
                // Play Windows system sound based on the selected sound effect
                // MessageBeep sound types: 0 = default, 0x10 = stop, 0x20 = question, 0x30 = exclamation, 0x40 = asterisk
                uint soundType = soundEffect switch
                {
                    1 => 0x30, // Exclamation
                    2 => 0x00, // Default beep
                    3 => 0x40, // Asterisk (informational)
                    4 => 0x20, // Question
                    5 => 0x10, // Stop/Error
                    _ => 0x30  // Default to exclamation
                };
                
                MessageBeep(soundType);
                Log.Information($"Playing Windows system sound {soundEffect} (type: 0x{soundType:X})");
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to play winner sound: {ex.Message}");
        }
    }

    public void PlayTestSound(int soundEffect)
    {
        PlayWinnerSound(soundEffect);
    }
    
    private string GetChatCommand(ChatChannel channel)
    {
        return channel switch
        {
            ChatChannel.Say => "/say ",
            ChatChannel.Party => "/party ",
            ChatChannel.Yell => "/yell ",
            ChatChannel.Shout => "/shout ",
            ChatChannel.Echo => "/echo ",
            _ => "/say "
        };
    }
    
    private void SendChatMessage(string message)
    {
        // For single messages, send immediately
        SendChatMessageImmediate(message);
        lastMessageSent = DateTime.Now;
    }
    
    private void SendChatMessageImmediate(string message)
    {
        try
        {
            var command = GetChatCommand(Configuration.AnnouncementChannel) + message;
            Chat.SendMessage(command);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to send chat message: {ex.Message}");
        }
    }
    
    private void QueueChatMessage(string message)
    {
        messageQueue.Enqueue(message);
    }
    

    public void StartGame()
    {
        if (isGameActive)
        {
            ChatGui.PrintError("[Spamroll] A game is already in progress!");
            return;
        }

        if (Configuration.WinningNumbers.Count == 0)
        {
            ChatGui.PrintError("[Spamroll] No winning numbers configured! Use /spamconfig to set them.");
            return;
        }

        lock (lockObject)
        {
            gameCancellation?.Cancel();
            gameCancellation = new CancellationTokenSource();

            isGameActive = true;
            isGamePaused = false;
            gameWinners.Clear();
            rollCounter = 0;
            gameStartTime = DateTime.Now;

            var activeWinningNumbers = Configuration.WinningNumbers.Take(Configuration.WinningNumberCount);
            var winningNumbersText = string.Join(", ", activeWinningNumbers.OrderBy(n => n));
            
            if (!Configuration.ManualMode)
            {
                if (Configuration.UseCustomTemplates)
                {
                    var startMessage = Configuration.GameStartTemplate.Replace("{numbers}", winningNumbersText);
                    SendChatMessage(startMessage);
                }
                else
                {
                    SendChatMessage($"[Spamroll] Game started! Winning numbers: {winningNumbersText}");
                }
                
                // Schedule instruction message for 2 seconds from now
                pendingInstructionTime = DateTime.Now.AddSeconds(2);
            }

            if (Configuration.RollTimeout > 0)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(Configuration.RollTimeout * 1000, gameCancellation.Token);
                        if (isGameActive && gameWinners.Count == 0)
                        {
                            if (!Configuration.ManualMode)
                            {
                                SendChatMessage("[Spamroll] Time's up! No winners this round.");
                            }
                            EndGame();
                        }
                    }
                    catch (OperationCanceledException) { }
                });
            }
        }
    }

    public void StopGame()
    {
        EndGame();
    }
    
    public void ClearMessageQueue()
    {
        var queuedCount = messageQueue.Count;
        messageQueue.Clear();
        lastMessageSent = DateTime.MinValue;
        Log.Information($"Message queue cleared (had {queuedCount} pending messages)");
    }

    private void EndGame()
    {
        if (!isGameActive)
        {
            ChatGui.PrintError("[Spamroll] No game is currently active.");
            return;
        }

        lock (lockObject)
        {
            gameCancellation?.Cancel();
            isGameActive = false;
            isGamePaused = false;

            var winnerCount = gameWinners.Count;

            if (!Configuration.ManualMode)
            {
                if (Configuration.UseCustomTemplates)
                {
                    var endMessage = Configuration.GameEndTemplate.Replace("{winnerCount}", winnerCount.ToString());
                    SendChatMessage(endMessage);
                }
                else if (Configuration.AllowMultipleWinners && winnerCount > 0)
                {
                    // Show detailed results for multiple winners
                    SendChatMessage($"[Spamroll] Game stopped. {winnerCount} winners:");
                    foreach (var winner in gameWinners.OrderBy(w => w.RollValue))
                    {
                        SendChatMessage($"  {winner.PlayerName} won {winner.RollValue}");
                    }
                }
                else
                {
                    SendChatMessage($"[Spamroll] Game stopped. {winnerCount} winners.");
                }
            }
        }
    }

    public bool IsGameActive => isGameActive;
    public bool IsGamePaused => isGamePaused;
    public IReadOnlyList<Winner> GetCurrentWinners() => gameWinners;
    
    public void PauseGame()
    {
        if (isGameActive && !isGamePaused)
        {
            isGamePaused = true;
            if (!Configuration.ManualMode)
            {
                SendChatMessage("[Spamroll] Game paused.");
            }
        }
    }
    
    public void ResumeGame()
    {
        if (isGameActive && isGamePaused)
        {
            isGamePaused = false;
            if (!Configuration.ManualMode)
            {
                SendChatMessage("[Spamroll] Game resumed.");
            }
        }
    }
    
    public void RestartGame()
    {
        if (isGameActive)
        {
            StopGame();
        }
        StartGame();
    }
    
    public void AnnounceWinners()
    {
        if (gameWinners.Count == 0)
        {
            SendChatMessage("[Spamroll] No winners to announce.");
            return;
        }
        
        // Queue the header message
        QueueChatMessage($"[Spamroll] Current winners ({gameWinners.Count}):");
        
        // Queue each winner message with proper spacing
        foreach (var winner in gameWinners.OrderBy(w => w.WinTime))
        {
            QueueChatMessage($"  {winner.PlayerName} - {winner.RollValue}");
        }
    }
    

    private void DrawUI() => WindowSystem.Draw();
    
    public string GetGameStartText()
    {
        var activeWinningNumbers = Configuration.WinningNumbers.Take(Configuration.WinningNumberCount);
        var winningNumbersText = string.Join(", ", activeWinningNumbers.OrderBy(n => n));
        
        if (Configuration.UseCustomTemplates)
        {
            return Configuration.GameStartTemplate.Replace("{numbers}", winningNumbersText);
        }
        else
        {
            return $"[Spamroll] Game started! Winning numbers: {winningNumbersText}";
        }
    }
    
    public string GetGameInstructionText()
    {
        return "[Spamroll] Players, type /random to participate!";
    }
    
    public string GetWinnerAnnouncementText(Winner winner)
    {
        var debugInfo = winner.IsDebugRoll ? " [DEBUG]" : "";
        if (Configuration.UseCustomTemplates)
        {
            var announcement = Configuration.WinnerAnnouncementTemplate
                .Replace("{player}", winner.PlayerName)
                .Replace("{roll}", winner.RollValue.ToString());
            return $"{announcement}{debugInfo}";
        }
        else
        {
            return $"WINNER: {winner.PlayerName} rolled {winner.RollValue}!{debugInfo}";
        }
    }
    
    public string GetGameEndText()
    {
        var winnerCount = gameWinners.Count;
        
        if (Configuration.UseCustomTemplates)
        {
            return Configuration.GameEndTemplate.Replace("{winnerCount}", winnerCount.ToString());
        }
        else if (Configuration.AllowMultipleWinners && winnerCount > 0)
        {
            var result = $"[Spamroll] Game stopped. {winnerCount} winners:\n";
            foreach (var winner in gameWinners.OrderBy(w => w.RollValue))
            {
                result += $"  {winner.PlayerName} won {winner.RollValue}\n";
            }
            return result.TrimEnd('\n');
        }
        else
        {
            return $"[Spamroll] Game stopped. {winnerCount} winners.";
        }
    }
    
    private void OnFrameworkUpdate()
    {
        // Check if we have a pending instruction message to send
        if (pendingInstructionTime.HasValue && DateTime.Now >= pendingInstructionTime.Value)
        {
            if (!Configuration.ManualMode)
            {
                SendChatMessage("[Spamroll] Players, type /random to participate!");
            }
            pendingInstructionTime = null; // Clear the pending message
        }
        
        // Process message queue with 2-second delays
        if (messageQueue.Count > 0)
        {
            var timeSinceLastMessage = DateTime.Now - lastMessageSent;
            if (timeSinceLastMessage.TotalMilliseconds >= 2000)
            {
                ProcessNextQueuedMessage();
            }
        }
    }
    
    private void ProcessNextQueuedMessage()
    {
        if (messageQueue.Count == 0) return;
        
        var message = messageQueue.Dequeue();
        SendChatMessageImmediate(message);
        lastMessageSent = DateTime.Now;
    }

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
