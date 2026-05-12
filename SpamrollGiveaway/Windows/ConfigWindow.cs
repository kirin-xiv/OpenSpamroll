using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Linq;

namespace SpamrollGiveaway.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;

    public ConfigWindow(Plugin plugin) : base("Spamroll Giveaway Configuration###SpamrollConfig", ImGuiWindowFlags.NoCollapse)
    {
        Size = new Vector2(400, 300);
        SizeCondition = ImGuiCond.FirstUseEver;

        Configuration = plugin.Configuration;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("ConfigTabs"))
        {
            if (ImGui.BeginTabItem("Game Settings"))
            {
                DrawGameSettings();
                ImGui.EndTabItem();
            }
            
            
            if (ImGui.BeginTabItem("Chat Templates"))
            {
                DrawChatTemplates();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Presets"))
            {
                DrawPresets();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
        
        // Footer for all tabs
        ImGui.Separator();
        ImGui.TextColored(new Vector4(1.0f, 0.75f, 0.8f, 1f), "Made with <3 by kirin-xiv");
    }
    
    private void DrawGameSettings()
    {
        ImGui.Text("Configure winning numbers:");
        ImGui.Separator();

        // Step 1: Let host choose HOW MANY winning numbers they want
        var winningCount = Configuration.WinningNumberCount;
        if (ImGui.SliderInt("Number of Winning Numbers", ref winningCount, 1, 9))
        {
            Configuration.WinningNumberCount = winningCount;
            
            // Resize the winning numbers list to match
            if (Configuration.WinningNumbers.Count < winningCount)
            {
                // Add default numbers (111, 222, 333, etc.)
                for (int i = Configuration.WinningNumbers.Count; i < winningCount; i++)
                {
                    Configuration.WinningNumbers.Add((i + 1) * 111);
                }
            }
            else if (Configuration.WinningNumbers.Count > winningCount)
            {
                // Remove excess numbers
                Configuration.WinningNumbers.RemoveRange(winningCount, Configuration.WinningNumbers.Count - winningCount);
            }
            
            Configuration.Save();
        }

        ImGui.Spacing();

        // Step 2: Show exactly that many input fields (vertical stack)
        ImGui.Text("Winning Numbers:");
        for (int i = 0; i < Configuration.WinningNumberCount; i++)
        {
            var valueStr = Configuration.WinningNumbers[i].ToString();
            if (ImGui.InputText($"Winning Number {i + 1}", ref valueStr, 3, ImGuiInputTextFlags.CharsDecimal))
            {
                if (int.TryParse(valueStr, out int value) && value >= 1 && value <= 999)
                {
                    Configuration.WinningNumbers[i] = value;
                    Configuration.Save();
                }
                else if (string.IsNullOrEmpty(valueStr))
                {
                    // Allow empty field temporarily but don't save invalid state
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Game Settings:");
        
        var rollTimeout = Configuration.RollTimeout;
        if (ImGui.SliderInt("Roll Timeout (seconds)", ref rollTimeout, 0, 300))
        {
            Configuration.RollTimeout = rollTimeout;
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("0 = No timeout");

        var autoClose = Configuration.AutoCloseAfterFirstWinner;
        if (ImGui.Checkbox("Auto-close game after first winner", ref autoClose))
        {
            Configuration.AutoCloseAfterFirstWinner = autoClose;
            Configuration.Save();
        }

        var allowMultiple = Configuration.AllowMultipleWinners;
        if (ImGui.Checkbox("Allow multiple winners (one per winning number)", ref allowMultiple))
        {
            Configuration.AllowMultipleWinners = allowMultiple;
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When enabled, each winning number can have its own winner\n(e.g., one winner for 111, one for 222, etc.)");

        // Only show this option if multiple winners is enabled
        if (Configuration.AllowMultipleWinners)
        {
            ImGui.Indent();
            var allowSamePlayer = Configuration.AllowSamePlayerMultipleWins;
            if (ImGui.Checkbox("Allow same player to win multiple numbers", ref allowSamePlayer))
            {
                Configuration.AllowSamePlayerMultipleWins = allowSamePlayer;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, the same player can win multiple winning numbers\n(e.g., Alice can win both 111 and 222)");
            ImGui.Unindent();
        }

        // Step 3: Add sound settings
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Sound Settings:");
        var enableSound = Configuration.EnableWinnerSound;
        if (ImGui.Checkbox("Enable Winner Sound", ref enableSound))
        {
            Configuration.EnableWinnerSound = enableSound;
            Configuration.Save();
        }

        if (Configuration.EnableWinnerSound)
        {
            // Sound type selection
            var soundTypes = new string[] { "Windows System Sound", "FFXIV In-Game Sound" };
            var selectedType = (int)Configuration.SoundType;
            if (ImGui.Combo("Sound Type", ref selectedType, soundTypes, soundTypes.Length))
            {
                Configuration.SoundType = (SoundEffectType)selectedType;
                Configuration.Save();
            }

            // Sound effect selection based on type
            if (Configuration.SoundType == SoundEffectType.WindowsSystemSound)
            {
                var soundEffects = new string[]
                {
                    "Sound 1 - Exclamation",
                    "Sound 2 - Default Beep", 
                    "Sound 3 - Asterisk (Info)",
                    "Sound 4 - Question",
                    "Sound 5 - Stop/Error"
                };

                var selectedSound = Configuration.SelectedSoundEffect - 1; // Convert to 0-based index
                if (selectedSound < 0 || selectedSound >= soundEffects.Length)
                    selectedSound = 0; // Default to first sound (Exclamation)

                if (ImGui.Combo("Winner Sound Effect", ref selectedSound, soundEffects, soundEffects.Length))
                {
                    Configuration.SelectedSoundEffect = selectedSound + 1; // Convert back to 1-based
                    Configuration.Save();
                }
            }
            else
            {
                // FFXIV sound effects (se.1 through se.16)
                var gameEffects = Enumerable.Range(1, 16).Select(i => $"<se.{i}>").ToArray();
                
                var selectedEffect = Configuration.SelectedSoundEffect - 1; // Convert to 0-based index
                if (selectedEffect < 0 || selectedEffect >= gameEffects.Length)
                    selectedEffect = 0; // Default to se.1

                if (ImGui.Combo("Winner Sound Effect", ref selectedEffect, gameEffects, gameEffects.Length))
                {
                    Configuration.SelectedSoundEffect = selectedEffect + 1; // Convert back to 1-based
                    Configuration.Save();
                }
            }

            // Test sound button
            if (ImGui.Button("Test Sound"))
            {
                Plugin.PlayTestSound(Configuration.SelectedSoundEffect);
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Debug Settings:");
        
        var debugMode = Configuration.DebugMode;
        if (ImGui.Checkbox("Debug Mode", ref debugMode))
        {
            Configuration.DebugMode = debugMode;
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Enables parsing of debug roll messages");

        var localPlayerName = Configuration.LocalPlayerName;
        if (ImGui.InputText("Local Player Name", ref localPlayerName, 50))
        {
            Configuration.LocalPlayerName = localPlayerName;
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Override for 'You' in roll messages");

        var movable = Configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            Configuration.IsConfigWindowMovable = movable;
            Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (Plugin.IsGameActive)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Game is currently active!");
            if (ImGui.Button("Stop Game"))
            {
                Plugin.StopGame();
            }
        }
        else
        {
            if (Configuration.WinningNumbers.Count == 0)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "No winning numbers set!");
            }
            else
            {
                if (ImGui.Button("Start Game"))
                {
                    Plugin.StartGame();
                }
            }
        }
    }
    
    
    private void DrawChatTemplates()
    {
        ImGui.Text("Chat Message Templates:");
        ImGui.Separator();
        
        // Manual mode checkbox
        var manualMode = Configuration.ManualMode;
        if (ImGui.Checkbox("Manual Mode", ref manualMode))
        {
            Configuration.ManualMode = manualMode;
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Disables automatic chat messages. Use copy/paste buttons instead.\nMakes plugin more resilient to game updates.");
        
        ImGui.Spacing();
        
        // Only show chat channel if not in manual mode
        if (!Configuration.ManualMode)
        {
            ImGui.Text("Announcement Channel:");
            var chatChannel = (int)Configuration.AnnouncementChannel;
            if (ImGui.Combo("##ChatChannel", ref chatChannel, "Say\0Party\0Yell\0Shout\0Echo\0"))
            {
                Configuration.AnnouncementChannel = (ChatChannel)chatChannel;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Chat channel for all game announcements");
            
            ImGui.Spacing();
        }
        else
        {
            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "Manual Mode: Chat automation disabled");
            ImGui.TextWrapped("Game messages will not be sent automatically. Use the copy/paste buttons in the main window.");
            ImGui.Spacing();
        }
        
        var useCustomTemplates = Configuration.UseCustomTemplates;
        if (ImGui.Checkbox("Use Custom Templates", ref useCustomTemplates))
        {
            Configuration.UseCustomTemplates = useCustomTemplates;
            Configuration.Save();
        }
        
        if (Configuration.UseCustomTemplates)
        {
            ImGui.Spacing();
            ImGui.Text("Available placeholders: {player}, {roll}, {numbers}, {winnerCount}");
            ImGui.Spacing();
            
            var winnerTemplate = Configuration.WinnerAnnouncementTemplate;
            if (ImGui.InputText("Winner Announcement", ref winnerTemplate, 200))
            {
                Configuration.WinnerAnnouncementTemplate = winnerTemplate;
                Configuration.Save();
            }
            
            var startTemplate = Configuration.GameStartTemplate;
            if (ImGui.InputText("Game Start Message", ref startTemplate, 200))
            {
                Configuration.GameStartTemplate = startTemplate;
                Configuration.Save();
            }
            
            var endTemplate = Configuration.GameEndTemplate;
            if (ImGui.InputText("Game End Message", ref endTemplate, 200))
            {
                Configuration.GameEndTemplate = endTemplate;
                Configuration.Save();
            }
            
            ImGui.Spacing();
            if (ImGui.Button("Reset to Defaults"))
            {
                Configuration.WinnerAnnouncementTemplate = "WINNER: {player} rolled {roll}!";
                Configuration.GameStartTemplate = "[Spamroll] Game started! Winning numbers: {numbers}";
                Configuration.GameEndTemplate = "[Spamroll] Game ended! {winnerCount} winners.";
                Configuration.Save();
            }
        }
        else
        {
            ImGui.TextDisabled("Using default chat messages");
        }
    }
    
    private void DrawPresets()
    {
        ImGui.Text("Game Presets:");
        ImGui.Separator();
        
        // Default presets
        if (ImGui.Button("Quick Roll (1-100)"))
        {
            ApplyPreset(new GamePreset
            {
                Name = "Quick Roll",
                WinningNumbers = new List<int> { 1, 50, 100 },
                WinningNumberCount = 3,
                AllowMultipleWinners = true,
                RollTimeout = 60
            });
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Triple Numbers"))
        {
            ApplyPreset(new GamePreset
            {
                Name = "Triple Numbers",
                WinningNumbers = new List<int> { 111, 222, 333, 444, 555, 666, 777, 888, 999 },
                WinningNumberCount = 9,
                AllowMultipleWinners = true,
                RollTimeout = 120
            });
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Single Winner"))
        {
            ApplyPreset(new GamePreset
            {
                Name = "Single Winner",
                WinningNumbers = new List<int> { 777 },
                WinningNumberCount = 1,
                AllowMultipleWinners = false,
                RollTimeout = 60
            });
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Custom presets
        ImGui.Text("Custom Presets:");
        
        if (Configuration.GamePresets.Count == 0)
        {
            ImGui.TextDisabled("No custom presets saved");
        }
        else
        {
            foreach (var preset in Configuration.GamePresets)
            {
                if (ImGui.Button($"Load: {preset.Name}"))
                {
                    ApplyPreset(preset);
                }
                ImGui.SameLine();
                if (ImGui.SmallButton($"Delete##{preset.Name}"))
                {
                    Configuration.GamePresets.Remove(preset);
                    Configuration.Save();
                    break;
                }
                if (!string.IsNullOrEmpty(preset.Description))
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"- {preset.Description}");
                }
            }
        }
        
        ImGui.Spacing();
        if (ImGui.Button("Save Current as Preset"))
        {
            var preset = new GamePreset
            {
                Name = $"Preset {Configuration.GamePresets.Count + 1}",
                WinningNumbers = new List<int>(Configuration.WinningNumbers),
                WinningNumberCount = Configuration.WinningNumberCount,
                AllowMultipleWinners = Configuration.AllowMultipleWinners,
                AllowSamePlayerMultipleWins = Configuration.AllowSamePlayerMultipleWins,
                RollTimeout = Configuration.RollTimeout
            };
            
            Configuration.GamePresets.Add(preset);
            Configuration.Save();
        }
    }
    
    private void ApplyPreset(GamePreset preset)
    {
        Configuration.WinningNumbers = new List<int>(preset.WinningNumbers);
        Configuration.WinningNumberCount = preset.WinningNumberCount;
        Configuration.AllowMultipleWinners = preset.AllowMultipleWinners;
        Configuration.AllowSamePlayerMultipleWins = preset.AllowSamePlayerMultipleWins;
        Configuration.RollTimeout = preset.RollTimeout;
        Configuration.Save();
    }
}
