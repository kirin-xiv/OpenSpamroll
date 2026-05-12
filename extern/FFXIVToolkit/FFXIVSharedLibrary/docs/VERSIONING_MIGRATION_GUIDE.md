# 🔄 FFXIVSharedLibrary Versioning Migration Guide

> **For Team Members:** How to migrate from our original PowerShell versioning to the FFXIVSharedLibrary automated versioning system.

---

## 📋 **Quick Overview**

**What we're doing:** Replacing our custom PowerShell versioning with a reusable shared library component that does the same thing, but better.

**Why:** 
- ✅ Less code duplication across projects
- ✅ Better error handling  
- ✅ More robust JSON processing
- ✅ Supports multiple JSON files automatically
- ✅ Easier maintenance (update once, benefits everyone)

---

## 🎯 **Migration Steps**

### **Step 1: Add FFXIVSharedLibrary Reference**

In your plugin's `.csproj` file, add this to your `<ItemGroup>` section:

```xml
<ItemGroup>
    <!-- Add this line - adjust path to where FFXIVSharedLibrary is located -->
    <ProjectReference Include="..\..\FFXIVToolkit\FFXIVSharedLibrary\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
    
    <!-- Your existing references stay the same -->
    <PackageReference Include="DalamudPackager" Version="2.1.13" />
    <!-- etc... -->
</ItemGroup>
```

### **Step 2: Remove Old Versioning Code**

**Delete this entire block from your .csproj:**

```xml
<!-- DELETE THIS ENTIRE SECTION -->
<Target Name="UpdateJsonVersions" BeforeTargets="BeforeBuild">
    <Message Text="Updating JSON files to version $(Version)..." Importance="high" />
    <Exec Command="powershell -Command &quot;$json = Get-Content '$(ProjectDir)FFToD.json' | ConvertFrom-Json; $json.AssemblyVersion = '$(Version)'; $json | ConvertTo-Json -Depth 10 | Set-Content '$(ProjectDir)FFToD.json'&quot;"
          ContinueOnError="false" />
    <Message Text="Updated FFToD.json to version $(Version)" Importance="high" />
</Target>
<!-- DELETE ABOVE -->
```

### **Step 3: Keep Your Version Property**

**Keep this exactly as it is:**

```xml
<PropertyGroup>
    <AssemblyName>YourPluginName</AssemblyName>
    <!-- Keep this - it's your single source of truth -->
    <Version>1.3.0.0</Version>
    <AssemblyVersion>$(Version)</AssemblyVersion>
    <FileVersion>$(Version)</FileVersion>
    <!-- other properties... -->
</PropertyGroup>
```

### **Step 4: Test the Migration**

1. **Clean your project:** `dotnet clean`
2. **Build your project:** `dotnet build`
3. **Check your JSON file(s):** The `AssemblyVersion` should now match your `<Version>` property

---

## 📁 **Complete Migration Example**

### **BEFORE (Your Original .csproj):**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Dalamud.NET.Sdk/12.0.0">
    <PropertyGroup>
        <AssemblyName>MyPlugin</AssemblyName>
        <Version>1.3.0.0</Version>
        <AssemblyVersion>$(Version)</AssemblyVersion>
        <FileVersion>$(Version)</FileVersion>
        <Authors>YourName</Authors>
        <Description>My awesome plugin</Description>
        <DalamudApiLevel>12</DalamudApiLevel>
        <TargetFramework>net9.0-windows</TargetFramework>
    </PropertyGroup>

    <!-- OLD VERSIONING - REMOVE THIS -->
    <Target Name="UpdateJsonVersions" BeforeTargets="BeforeBuild">
        <Message Text="Updating JSON files to version $(Version)..." Importance="high" />
        <Exec Command="powershell -Command &quot;$json = Get-Content '$(ProjectDir)MyPlugin.json' | ConvertFrom-Json; $json.AssemblyVersion = '$(Version)'; $json | ConvertTo-Json -Depth 10 | Set-Content '$(ProjectDir)MyPlugin.json'&quot;"
              ContinueOnError="false" />
        <Message Text="Updated MyPlugin.json to version $(Version)" Importance="high" />
    </Target>
</Project>
```

### **AFTER (Using FFXIVSharedLibrary):**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Dalamud.NET.Sdk/12.0.0">
    <PropertyGroup>
        <AssemblyName>MyPlugin</AssemblyName>
        <Version>1.3.0.0</Version>
        <AssemblyVersion>$(Version)</AssemblyVersion>
        <FileVersion>$(Version)</FileVersion>
        <Authors>YourName</Authors>
        <Description>My awesome plugin</Description>
        <DalamudApiLevel>12</DalamudApiLevel>
        <TargetFramework>net9.0-windows</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <!-- NEW: Add FFXIVSharedLibrary reference -->
        <ProjectReference Include="..\..\FFXIVToolkit\FFXIVSharedLibrary\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
    </ItemGroup>
    
    <!-- That's it! Versioning happens automatically -->
</Project>
```

**Result:** Same behavior, 90% less code! 🎉

---

## 🎁 **Bonus: What Else You Get**

By adding FFXIVSharedLibrary, you also get access to:

### **Player Name Utilities:**
```csharp
using FFXIVSharedLibrary.Player;

// Automatically handle server suffixes
var cleanName = PlayerNameNormalizer.NormalizeName("Player Name Gilgamesh");
// Result: "Player Name"

// Handle "You" case
var playerName = PlayerNameNormalizer.NormalizeNameWithLocalPlayer("You", localPlayerName);
```

### **Server Data:**
```csharp
// Check if server exists
bool isValid = ServerData.IsValidServer("Gilgamesh"); // true

// Find datacenter and region
string datacenter = ServerData.GetDatacenterForServer("Gilgamesh"); // "Aether"
string region = ServerData.GetRegionForServer("Gilgamesh"); // "North America"
```

### **Chat Processing:**
```csharp
using FFXIVSharedLibrary.Chat;

var rollHandler = new RollHandler(localPlayerName);
rollHandler.RollDetected += (roll) => {
    chatGui.Print($"{roll.NormalizedPlayerName} rolled {roll.RollValue}!");
};
```

---

## 📂 **Path Examples for Different Project Structures**

### **If your project is in the same repo as FFXIVToolkit:**
```
FFXIVToolkit/
├── FFXIVSharedLibrary/
└── YourPlugin/
    └── YourPlugin.csproj
```

**Use:** `<ProjectReference Include="..\FFXIVSharedLibrary\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />`

### **If your project is in a subfolder:**
```
FFXIVToolkit/
├── FFXIVSharedLibrary/
└── Plugins/
    └── YourPlugin/
        └── YourPlugin.csproj
```

**Use:** `<ProjectReference Include="..\..\FFXIVSharedLibrary\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />`

### **If your project is in a completely different directory:**
```
SomeOtherDirectory/
└── YourPlugin/
    └── YourPlugin.csproj

FFXIVToolkit/
└── FFXIVSharedLibrary/
```

**Use:** `<ProjectReference Include="..\..\FFXIVToolkit\FFXIVSharedLibrary\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />`

---

## ✅ **Verification Checklist**

After migration, verify everything works:

- [ ] **Project builds successfully** (`dotnet build`)
- [ ] **JSON file(s) updated** (check that `AssemblyVersion` matches your `<Version>`)
- [ ] **No build errors** related to versioning
- [ ] **Plugin loads in game** (if applicable)

---

## 🆘 **Need Help?**

- **Build errors?** See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Path issues?** Double-check the relative path to FFXIVSharedLibrary
- **Questions?** Ask in team chat or create an issue

---

## 🎉 **You're Done!**

Your versioning now works the same as before, but with:
- ✅ Less code to maintain
- ✅ Better error handling
- ✅ Access to shared utilities
- ✅ Automatic updates when the library improves

Welcome to the shared library! 🚀