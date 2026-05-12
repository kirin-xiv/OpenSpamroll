# 🛠️ FFXIVSharedLibrary Troubleshooting Guide

> **Common issues and solutions** when using FFXIVSharedLibrary components.

---

## 🔧 **Versioning Issues**

### **❌ Problem: JSON files not updating during build**

**Symptoms:**
- Build succeeds but `AssemblyVersion` in JSON stays the same
- No versioning messages in build output

**Solutions:**

1. **Check project reference:**
   ```xml
   <!-- Make sure this exists in your .csproj -->
   <ItemGroup>
       <ProjectReference Include="path\to\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
   </ItemGroup>
   ```

2. **Verify file naming:**
   - JSON file should be named `YourAssemblyName.json` or `Plugin.json`
   - Check `<AssemblyName>` property in your .csproj

3. **Enable versioning explicitly:**
   ```xml
   <PropertyGroup>
       <EnableAutoVersioning>true</EnableAutoVersioning>
   </PropertyGroup>
   ```

4. **Check build output for errors:**
   - Look for PowerShell execution errors
   - Ensure PowerShell is available on the system

---

### **❌ Problem: Build fails with PowerShell errors**

**Error messages:**
- `PowerShell command failed`
- `ConvertFrom-Json` errors
- Access denied to JSON files

**Solutions:**

1. **Check JSON file permissions:**
   - Ensure JSON files are not read-only
   - Check file is not locked by another process

2. **Validate JSON syntax:**
   ```json
   {
     "Author": "YourName",
     "Name": "Your Plugin",
     "AssemblyVersion": "1.0.0.0"
   }
   ```
   - Use a JSON validator to check syntax
   - Ensure no trailing commas or syntax errors

3. **Disable versioning temporarily:**
   ```xml
   <PropertyGroup>
       <EnableAutoVersioning>false</EnableAutoVersioning>
   </PropertyGroup>
   ```

4. **Use C# versioning instead:**
   ```csharp
   // In your build script or MSBuild task
   using FFXIVSharedLibrary.Build;
   VersioningHelper.UpdateJsonVersion("Plugin.json", "1.0.0");
   ```

---

## 🎭 **Player Name Issues**

### **❌ Problem: Player names not normalizing correctly**

**Symptoms:**
- Server names not being removed
- "You" not converting to player name
- Names with special characters failing

**Solutions:**

1. **Check server name format:**
   ```csharp
   // These should work:
   var name1 = PlayerNameNormalizer.NormalizeName("Player Name Gilgamesh");  // ✅
   var name2 = PlayerNameNormalizer.NormalizeName("PlayerNameGilgamesh");   // ✅
   
   // These won't work (not real FFXIV formats):
   var name3 = PlayerNameNormalizer.NormalizeName("Player@Gilgamesh");      // ❌
   ```

2. **Handle "You" case properly:**
   ```csharp
   var localPlayerName = clientState.LocalPlayer?.Name.TextValue;
   var normalized = PlayerNameNormalizer.NormalizeNameWithLocalPlayer("You", localPlayerName);
   ```

3. **Verify server exists:**
   ```csharp
   bool isValid = ServerData.IsValidServer("Gilgamesh"); // Should be true
   ```

---

### **❌ Problem: Server not found in ServerData**

**Symptoms:**
- `IsValidServer()` returns false for valid servers
- Server lookups returning null

**Solutions:**

1. **Check server name spelling:**
   ```csharp
   // Correct spellings:
   ServerData.IsValidServer("Gilgamesh");    // ✅
   ServerData.IsValidServer("Midgardsormr"); // ✅
   
   // Common misspellings:
   ServerData.IsValidServer("Gilgemesh");    // ❌
   ServerData.IsValidServer("Midgard");      // ❌
   ```

2. **Case sensitivity:**
   ```csharp
   // These all work (case insensitive):
   ServerData.IsValidServer("GILGAMESH");   // ✅
   ServerData.IsValidServer("gilgamesh");   // ✅
   ServerData.IsValidServer("Gilgamesh");   // ✅
   ```

3. **Check if server is in our database:**
   ```csharp
   // List all servers to verify
   foreach (var server in ServerData.AllServers)
   {
       Console.WriteLine(server);
   }
   ```

---

## 💬 **Chat Processing Issues**

### **❌ Problem: Roll detection not working**

**Symptoms:**
- Roll messages not triggering events
- `RollDetected` event never fires

**Solutions:**

1. **Check message format:**
   ```csharp
   // These should work:
   "Random! Player Name rolls a 42."     // ✅
   "Random! You roll a 78."              // ✅
   
   // These won't work:
   "Player Name rolled 42"               // ❌ (wrong format)
   "Random! Player rolled a 42"          // ❌ (missing 's' in 'rolls')
   ```

2. **Verify handler registration:**
   ```csharp
   var processor = new ChatMessageProcessor();
   var rollHandler = new RollHandler(localPlayerName);
   
   // Make sure this line exists:
   processor.RegisterHandler(rollHandler);
   
   // And this event is subscribed:
   rollHandler.RollDetected += OnRollDetected;
   ```

3. **Check chat message processing:**
   ```csharp
   private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
   {
       // Make sure this line exists and is called:
       chatProcessor.ProcessMessage((int)type, timestamp, sender.TextValue, message.TextValue);
   }
   ```

---

### **❌ Problem: Custom chat handlers not working**

**Symptoms:**
- Custom `RegexChatHandler` not triggering
- Messages not matching expected patterns

**Solutions:**

1. **Test regex pattern:**
   ```csharp
   var pattern = @"!mycommand (.+)";
   var message = "!mycommand hello world";
   var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
   Console.WriteLine($"Match: {match.Success}"); // Should be true
   ```

2. **Check handler priority:**
   ```csharp
   public class MyHandler : RegexChatHandler
   {
       public override int Priority => 100; // Higher numbers processed first
   }
   ```

3. **Verify handler registration order:**
   ```csharp
   // Register higher priority handlers first
   processor.RegisterHandler(highPriorityHandler);
   processor.RegisterHandler(lowPriorityHandler);
   ```

---

## 🎮 **Game Session Issues**

### **❌ Problem: Game session not starting**

**Symptoms:**
- `StartSession()` returns false
- Session remains in `Inactive` state

**Solutions:**

1. **Check if session already active:**
   ```csharp
   if (gameSession.IsActive)
   {
       gameSession.StopSession(); // Stop current session first
   }
   
   gameSession.StartSession(new MyGameState());
   ```

2. **Verify state object:**
   ```csharp
   // Make sure your state class has parameterless constructor:
   public class MyGameState
   {
       public MyGameState() { } // ✅ Required
       
       public Dictionary<string, int> Rolls { get; set; } = new();
   }
   ```

---

### **❌ Problem: Session timeout not working**

**Symptoms:**
- Sessions don't auto-stop after timeout
- `StartSessionWithTimeout()` not working

**Solutions:**

1. **Check timeout duration:**
   ```csharp
   // Make sure timeout is reasonable:
   gameSession.StartSessionWithTimeout(TimeSpan.FromSeconds(30));  // ✅
   
   // Not this:
   gameSession.StartSessionWithTimeout(TimeSpan.FromMilliseconds(1)); // ❌ Too short
   ```

2. **Verify cancellation token:**
   ```csharp
   // Check if cancellation token is being honored
   var token = gameSession.CancellationToken;
   Console.WriteLine($"Is cancelled: {token.IsCancellationRequested}");
   ```

---

## ⚙️ **Configuration Issues**

### **❌ Problem: Configuration not saving/loading**

**Symptoms:**
- Settings reset after restart
- `Save()` method not persisting data
- `Load()` method not reading file

**Solutions:**

1. **Check file path:**
   ```csharp
   public class MyConfig : JsonFileConfiguration
   {
       public MyConfig() : base(GetConfigPath()) { Load(); }
       
       private static string GetConfigPath()
       {
           var path = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
               "MyPlugin",
               "config.json"
           );
           
           // Ensure directory exists
           Directory.CreateDirectory(Path.GetDirectoryName(path)!);
           return path;
       }
   }
   ```

2. **Verify file permissions:**
   ```csharp
   try
   {
       config.Save();
   }
   catch (UnauthorizedAccessException ex)
   {
       Console.WriteLine($"No write permission: {ex.Message}");
   }
   catch (DirectoryNotFoundException ex)
   {
       Console.WriteLine($"Directory doesn't exist: {ex.Message}");
   }
   ```

3. **Check JSON serialization:**
   ```csharp
   // Properties must have getters and setters:
   public class MyConfig : JsonFileConfiguration
   {
       public int Timeout { get; set; } = 30;        // ✅ Works
       public readonly int ReadOnly = 30;            // ❌ Won't serialize
       private int privateField = 30;               // ❌ Won't serialize
   }
   ```

---

## 🚀 **Build and Compilation Issues**

### **❌ Problem: Reference errors when building**

**Error messages:**
- `The type or namespace 'FFXIVSharedLibrary' could not be found`
- `Assembly reference missing`

**Solutions:**

1. **Check project reference path:**
   ```xml
   <!-- Verify path is correct relative to your .csproj -->
   <ProjectReference Include="..\..\FFXIVToolkit\FFXIVSharedLibrary\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
   ```

2. **Use absolute path temporarily:**
   ```xml
   <ProjectReference Include="C:\path\to\FFXIVSharedLibrary\FFXIVSharedLibrary.csproj" />
   ```

3. **Clean and rebuild:**
   ```bash
   dotnet clean
   dotnet restore
   dotnet build
   ```

---

### **❌ Problem: Version conflicts**

**Error messages:**
- Assembly version conflicts
- Framework version mismatches

**Solutions:**

1. **Check target framework:**
   ```xml
   <!-- Both projects should target the same framework -->
   <TargetFramework>net9.0-windows</TargetFramework>
   ```

2. **Verify package versions:**
   ```xml
   <!-- Use same Dalamud SDK version -->
   <Project Sdk="Dalamud.NET.Sdk/12.0.0">
   ```

---

## 📞 **Getting Additional Help**

### **Debug Information to Gather:**

1. **Build output** (full log with detailed verbosity)
2. **Your .csproj file** content
3. **File structure** of your project
4. **Error messages** (exact text)
5. **FFXIVSharedLibrary version** being used

### **Enable Detailed Logging:**

```bash
# Build with detailed output
dotnet build --verbosity detailed

# Or diagnostic level for maximum info
dotnet build --verbosity diagnostic
```

### **Common Log Locations:**

- **MSBuild logs:** Usually in `obj/` folder
- **Plugin logs:** Check Dalamud log directory
- **Configuration files:** Check expected save locations

---

## ✅ **Quick Health Check**

Run this checklist to verify everything is working:

- [ ] **Build succeeds** without errors
- [ ] **JSON versioning** updates files during build
- [ ] **Player names** normalize correctly
- [ ] **Roll detection** triggers events
- [ ] **Game sessions** start and stop properly
- [ ] **Configuration** saves and loads
- [ ] **All using statements** resolve correctly

If all items pass, your integration is working correctly! 🎉

---

## 🆘 **Still Need Help?**

- **Check our examples** in the [Usage Guide](USAGE_GUIDE.md)
- **Review migration steps** in [Migration Guide](VERSIONING_MIGRATION_GUIDE.md)
- **Ask the team** in your development chat
- **Create an issue** with detailed reproduction steps

We're here to help! 🚀