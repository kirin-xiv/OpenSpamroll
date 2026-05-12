using FFXIVSharedLibrary.Player;
using System.Text.RegularExpressions;

namespace FFXIVSharedLibrary.Chat;

public delegate void RollDetectedHandler(RollEventArgs args);

public class RollEventArgs
{
    public required string PlayerName { get; init; }
    public required string NormalizedPlayerName { get; init; }
    public required int RollValue { get; init; }
    public required int Timestamp { get; init; }
    public required string OriginalMessage { get; init; }
    public bool IsDebugRoll { get; init; } = false;
    public int? MaxRollValue { get; init; } = null;
    public int RollOrder { get; init; } = 0;
}

public class RollHandler : IChatMessageHandler
{
    private readonly string? localPlayerName;
    private readonly Regex normalPattern;
    private readonly Regex debugPattern;
    private readonly object lockObject = new object();
    private bool debugMode;
    private int rollCounter = 0;

    public int Priority => 100;
    public event RollDetectedHandler? RollDetected;

    public RollHandler(string? localPlayerName = null, bool debugMode = false)
    {
        this.localPlayerName = localPlayerName;
        this.debugMode = debugMode;
        
        // Pre-compiled regex patterns for performance
        normalPattern = new Regex(@"Random! (.+) rolls? a (\d+)\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        debugPattern = new Regex(@"Random! (.+) rolls? a (\d+) \(out of (\d+)\)\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public bool CanHandle(ChatMessageEventArgs args)
    {
        lock (lockObject)
        {
            if (debugMode)
            {
                return normalPattern.IsMatch(args.Message) || debugPattern.IsMatch(args.Message);
            }
            return normalPattern.IsMatch(args.Message);
        }
    }

    public void Handle(ChatMessageEventArgs args)
    {
        lock (lockObject)
        {
            Match match;
            bool isDebugRoll = false;
            int? maxRollValue = null;

            // Try debug pattern first if debug mode is enabled
            if (debugMode && (match = debugPattern.Match(args.Message)).Success)
            {
                isDebugRoll = true;
                maxRollValue = int.Parse(match.Groups[3].Value);
            }
            // Try normal pattern
            else if ((match = normalPattern.Match(args.Message)).Success)
            {
                isDebugRoll = false;
                maxRollValue = null;
            }
            else
            {
                return; // No match found
            }

            var playerName = match.Groups[1].Value.Trim();
            var rollValue = int.Parse(match.Groups[2].Value);
            var normalizedName = PlayerNameNormalizer.NormalizeNameWithLocalPlayer(playerName, localPlayerName);

            var rollEventArgs = new RollEventArgs
            {
                PlayerName = playerName,
                NormalizedPlayerName = normalizedName,
                RollValue = rollValue,
                Timestamp = args.Timestamp,
                OriginalMessage = args.Message,
                IsDebugRoll = isDebugRoll,
                MaxRollValue = maxRollValue,
                RollOrder = rollCounter++
            };

            RollDetected?.Invoke(rollEventArgs);
        }
    }

    public void SetDebugMode(bool enabled)
    {
        lock (lockObject)
        {
            debugMode = enabled;
        }
    }

    public bool GetDebugMode()
    {
        lock (lockObject)
        {
            return debugMode;
        }
    }

    public void UpdateLocalPlayerName(string? newLocalPlayerName)
    {
        if (localPlayerName != newLocalPlayerName)
        {
            var newHandler = new RollHandler(newLocalPlayerName, debugMode);
            newHandler.RollDetected += (args) => RollDetected?.Invoke(args);
        }
    }
}

public class RollCollector
{
    private readonly Dictionary<string, RollEventArgs> rolls = new();
    private readonly object lockObject = new object();

    public event RollDetectedHandler? NewRollAdded;
    public event Action<Dictionary<string, RollEventArgs>>? RollsCleared;

    public bool AddRoll(RollEventArgs rollArgs)
    {
        lock (lockObject)
        {
            if (rolls.ContainsKey(rollArgs.NormalizedPlayerName))
                return false;

            rolls[rollArgs.NormalizedPlayerName] = rollArgs;
            NewRollAdded?.Invoke(rollArgs);
            return true;
        }
    }

    public bool HasRoll(string normalizedPlayerName)
    {
        lock (lockObject)
        {
            return rolls.ContainsKey(normalizedPlayerName);
        }
    }

    public RollEventArgs? GetRoll(string normalizedPlayerName)
    {
        lock (lockObject)
        {
            return rolls.GetValueOrDefault(normalizedPlayerName);
        }
    }

    public Dictionary<string, RollEventArgs> GetAllRolls()
    {
        lock (lockObject)
        {
            return new Dictionary<string, RollEventArgs>(rolls);
        }
    }

    public int GetRollCount()
    {
        lock (lockObject)
        {
            return rolls.Count;
        }
    }

    public (string playerName, int rollValue)? GetHighestRoll()
    {
        lock (lockObject)
        {
            if (rolls.Count == 0) return null;

            var highest = rolls.Values.OrderByDescending(r => r.RollValue).First();
            return (highest.NormalizedPlayerName, highest.RollValue);
        }
    }

    public int GetDebugRollCount()
    {
        lock (lockObject)
        {
            return rolls.Values.Count(r => r.IsDebugRoll);
        }
    }

    public List<RollEventArgs> GetTiedRolls()
    {
        lock (lockObject)
        {
            if (rolls.Count == 0) return new List<RollEventArgs>();

            var maxValue = rolls.Values.Max(r => r.RollValue);
            var tiedRolls = rolls.Values
                .Where(r => r.RollValue == maxValue)
                .OrderBy(r => r.RollOrder) // First roller wins for tiebreaker
                .ToList();

            return tiedRolls;
        }
    }

    public RollEventArgs? GetWinnerWithTiebreaker()
    {
        lock (lockObject)
        {
            var tiedRolls = GetTiedRolls();
            return tiedRolls.FirstOrDefault(); // First roller wins automatically
        }
    }

    public void ClearRolls()
    {
        lock (lockObject)
        {
            var rollsCopy = new Dictionary<string, RollEventArgs>(rolls);
            rolls.Clear();
            RollsCleared?.Invoke(rollsCopy);
        }
    }
}