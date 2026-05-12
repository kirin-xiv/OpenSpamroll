# 🎮 FFXIVSharedLibrary

A reusable library for FFXIV plugin development, providing common utilities for player name normalization, chat processing, game state management, and automated versioning.

## 🚀 **Quick Start**

### **Add to your project:**
```xml
<ItemGroup>
    <ProjectReference Include="path\to\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
</ItemGroup>
```

### **Get automatic versioning:**
```xml
<PropertyGroup>
    <Version>1.5.0.0</Version>  <!-- Change this to update all JSON files -->
</PropertyGroup>
```

### **Use the utilities:**
```csharp
using FFXIVSharedLibrary.Player;
using FFXIVSharedLibrary.Chat;

// Clean player names
var cleanName = PlayerNameNormalizer.NormalizeName("Player Name Gilgamesh");

// Handle rolls automatically  
var rollHandler = new RollHandler(localPlayerName);
rollHandler.RollDetected += (roll) => Console.WriteLine($"{roll.NormalizedPlayerName}: {roll.RollValue}");
```

## 📚 **Documentation**

- **[📖 Full Documentation](docs/README.md)** - Complete guide and reference
- **[🔄 Migration Guide](docs/VERSIONING_MIGRATION_GUIDE.md)** - **For team members:** Replace custom versioning
- **[⚡ Quick Reference](docs/QUICK_REFERENCE.md)** - Copy-paste code snippets
- **[🛠️ Troubleshooting](docs/TROUBLESHOOTING.md)** - Common issues and solutions

## 🎯 **What's Included**

- **🔧 Automated Versioning** - Single source of truth for version numbers across JSON files
- **🎭 Player Management** - Name normalization and comprehensive server data (85+ servers)
- **💬 Chat Processing** - Extensible message handling with roll detection
- **🎮 Game Sessions** - Thread-safe state management with timeouts
- **⚙️ Configuration** - JSON and memory-based configuration systems

## 💡 **Benefits**

✅ **Reduce code duplication** - Reuse common FFXIV plugin functionality  
✅ **Automated maintenance** - Server lists and versioning handled for you  
✅ **Better reliability** - Tested, thread-safe components  
✅ **Easy integration** - Drop-in replacement for existing patterns  

## 🎉 **Perfect For**

- Mini-game plugins (dice games, lotteries, contests)
- Chat automation tools
- Player management utilities
- Any plugin needing roll detection or server data

---

**Ready to get started?** Check out the [Migration Guide](docs/VERSIONING_MIGRATION_GUIDE.md) if you're upgrading from custom versioning, or the [Usage Guide](docs/USAGE_GUIDE.md) for new projects!