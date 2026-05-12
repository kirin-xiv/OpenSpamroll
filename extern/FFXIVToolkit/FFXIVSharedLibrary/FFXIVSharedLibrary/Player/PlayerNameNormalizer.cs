namespace FFXIVSharedLibrary.Player;

public static class PlayerNameNormalizer
{
    public static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        foreach (var server in ServerData.AllServers)
        {
            // Handle "Name Server" format (space-separated)
            if (name.EndsWith($" {server}", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - server.Length - 1);

            // Handle "NameServer" format (no separator) 
            if (name.EndsWith(server, StringComparison.OrdinalIgnoreCase))
            {
                var index = name.LastIndexOf(server, StringComparison.OrdinalIgnoreCase);
                return name.Substring(0, index).Trim();
            }
        }

        return name;
    }

    public static string NormalizeNameWithLocalPlayer(string name, string? localPlayerName = null)
    {
        var normalizedName = NormalizeName(name);

        if (normalizedName == "You" && !string.IsNullOrWhiteSpace(localPlayerName))
        {
            return localPlayerName;
        }

        return normalizedName;
    }
}