namespace FFXIVSharedLibrary.Player;

public static class ServerData
{
    public static readonly HashSet<string> AllServers = new()
    {
        "Adamantoise", "Aegis", "Alexander", "Alpha", "Anima", "Asura", "Atomos", "Bahamut",
        "Balmung", "Behemoth", "Belias", "Brynhildr", "Cactuar", "Carbuncle", "Cerberus",
        "Chocobo", "Coeurl", "Cuchulainn", "Diabolos", "Durandal", "Dynamis", "Excalibur",
        "Exodus", "Faerie", "Famfrit", "Fenrir", "Garuda", "Gilgamesh", "Goblin", "Gungnir",
        "Hades", "Halicarnassus", "Hifumi", "Hyperion", "Ifrit", "Ixion", "Jenova", "Kujata",
        "Lamia", "Leviathan", "Lich", "Louisoix", "Maduin", "Maelia", "Malboro", "Mandragora",
        "Masamune", "Mateus", "Midgardsormr", "Moogle", "Odin", "Omega", "Pandaemonium",
        "Phantom", "Phoenix", "Rafflesia", "Ragnarok", "Ramuh", "Ravana", "Raiden", "Ridill",
        "Sagittarius", "Sargatanas", "Seraph", "Shinryu", "Shiva", "Siren", "Sophia", "Spriggan",
        "Sphene", "Tiamat", "Titan", "Tonberry", "Tulimshar", "Typhon", "Ultima", "Ultros",
        "Unicorn", "Valefor", "Varis", "Yojimbo", "Zalera", "Zeromus", "Zodiark", "Zurvan"
    };

    public static readonly Dictionary<string, HashSet<string>> DatacenterToServers = new()
    {
        // North America
        ["Aether"] = new() { "Adamantoise", "Cactuar", "Faerie", "Gilgamesh", "Jenova", "Midgardsormr", "Sargatanas", "Siren" },
        ["Crystal"] = new() { "Balmung", "Brynhildr", "Coeurl", "Diabolos", "Goblin", "Malboro", "Mateus", "Zalera" },
        ["Dynamis"] = new() { "Halicarnassus", "Maduin", "Maelia", "Rafflesia", "Seraph", "Cuchulainn" },
        ["Primal"] = new() { "Behemoth", "Excalibur", "Exodus", "Famfrit", "Hyperion", "Lamia", "Leviathan", "Ultros" },

        // Europe
        ["Chaos"] = new() { "Cerberus", "Louisoix", "Moogle", "Omega", "Phantom", "Ragnarok", "Sagittarius", "Spriggan" },
        ["Light"] = new() { "Alpha", "Lich", "Odin", "Phoenix", "Raiden", "Shiva", "Twintania", "Zodiark" },

        // Japan
        ["Elemental"] = new() { "Aegis", "Atomos", "Carbuncle", "Garuda", "Gungnir", "Kujata", "Ramuh", "Tonberry" },
        ["Gaia"] = new() { "Alexander", "Bahamut", "Durandal", "Fenrir", "Ifrit", "Ridill", "Tiamat", "Ultima" },
        ["Mana"] = new() { "Anima", "Asura", "Belias", "Chocobo", "Hades", "Ixion", "Mandragora", "Masamune", "Pandaemonium", "Shinryu", "Titan" },
        ["Meteor"] = new() { "Belias", "Mandragora", "Masamune", "Pandaemonium", "Shinryu", "Titan", "Valefor", "Yojimbo", "Zeromus" },

        // Oceania
        ["Materia"] = new() { "Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan" },

        // China
        ["LuXingNiao"] = new() { "HongYuHai", "ShenYiZhiDi", "LaNuoXiYa", "HuanYingQunDao", "MengYaChi", "YuZhouHeYin", "WoXianXiRan", "ChenXiWangZuo" },
        ["MoGuLi"] = new() { "BaiYinXiang", "BaiJinHuanXiang", "ShenQuanHen", "YanXia", "JingYuZhuangYuan", "MoDuNa", "ZiShuiZhanQiao", "YanYang" },
        ["MaoXiaoPang"] = new() { "FuXiaoDao", "Longchaoshendian", "MengYuBaoJing", "ShenYiZhiDi", "TaiYangHaiAn", "YiXiuJiaDe", "HaiMaoChaWu", "RouFengHaiWan" },

        // Korea  
        ["한국"] = new() { "초코보", "모그리", "톤베리", "펜리르", "카벙클", "바하무트" }
    };

    public static readonly Dictionary<string, string> RegionToDatacenters = new()
    {
        ["North America"] = "Aether,Crystal,Dynamis,Primal",
        ["Europe"] = "Chaos,Light", 
        ["Japan"] = "Elemental,Gaia,Mana,Meteor",
        ["Oceania"] = "Materia",
        ["China"] = "LuXingNiao,MoGuLi,MaoXiaoPang",
        ["Korea"] = "한국"
    };

    public static bool IsValidServer(string serverName)
    {
        return AllServers.Contains(serverName);
    }

    public static string? GetDatacenterForServer(string serverName)
    {
        foreach (var (datacenter, servers) in DatacenterToServers)
        {
            if (servers.Contains(serverName))
                return datacenter;
        }
        return null;
    }

    public static string? GetRegionForServer(string serverName)
    {
        var datacenter = GetDatacenterForServer(serverName);
        if (datacenter == null) return null;

        foreach (var (region, datacenters) in RegionToDatacenters)
        {
            if (datacenters.Split(',').Contains(datacenter))
                return region;
        }
        return null;
    }

    public static HashSet<string> GetServersInDatacenter(string datacenter)
    {
        return DatacenterToServers.GetValueOrDefault(datacenter, new HashSet<string>());
    }

    public static HashSet<string> GetServersInRegion(string region)
    {
        var servers = new HashSet<string>();
        var datacenters = RegionToDatacenters.GetValueOrDefault(region, "").Split(',');
        
        foreach (var datacenter in datacenters)
        {
            if (!string.IsNullOrWhiteSpace(datacenter) && DatacenterToServers.ContainsKey(datacenter))
            {
                foreach (var server in DatacenterToServers[datacenter])
                {
                    servers.Add(server);
                }
            }
        }
        
        return servers;
    }
}