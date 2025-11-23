# Dynamic Worlds
### Persistent World Progression + Regenerating Worlds for Terraria (tModLoader)

Dynamic Worlds is a tModLoader utility mod that allows players to regenerate their world’s terrain without losing their game progression. The mod stores world state — including bosses defeated, Hardmode, ore tiers, and invasion progress — in a JSON snapshot file that persists across regenerations.

---

## ✨ Features

### 🔁 World Regeneration with Progress Persistence
Regenerate your world’s layout at any time using:
```
/regenworld
```
Terrain is rebuilt using a new random seed, while your progression is restored instantly.

### 🧠 Snapshot System
The mod stores:
- Hardmode status  
- World evil type (Crimson/Corruption)  
- Game Mode (Classic/Expert/Master/Journey)  
- Boss & event progression  
- Ore tiers (both pre-Hardmode and Hardmode)  
- Invasion completions  
- World identity (`worldName`, `worldID`, `worldSeed`)

### 🧱 Upcoming Feature: Permanent User Structures
Future versions will allow players to mark regions or builds as *persistent*, allowing structures to survive terrain regeneration while the world around them refreshes.

---

## 📜 Commands

| Command | Description |
|--------|-------------|
| `/regenworld` | Regenerates world terrain and reapplies progression |
| `/snap` | Prints the current world progression snapshot |
| `/hardmode on/off` | Force toggles Hardmode |
| `/down <boss/event>` | Marks a boss or event as defeated |

---

## 🛠 Technical Overview

The mod hooks into world lifecycle events to handle saving/loading automatically.  
Snapshot files are placed next to the world file:

```
YourWorld.wld
YourWorld_progress.json
```

If unavailable, a fallback path is used inside:
```
tModLoader/DynamicWorlds/WorldProgress.json
```

Regeneration steps:
1. Capture progression snapshot  
2. Clear world  
3. Reroll world using vanilla worldgen  
4. Restore snapshot  
5. Report differences before/after  
6. Teleport player to new spawn  

---

## 📦 Installation
Place the mod folder inside:
```
tModLoader/ModSources/DynamicWorlds
```
Build from in-game Mod Sources or via:
```
dotnet build
```

---

## 🤝 Contributing
Pull requests, suggestions, and issue reports are welcome.  
Permanent structure persistence is an upcoming major feature — contributions toward region saving/loading are appreciated.

---

## ❤️ Support
If you're using this mod from Steam, consider rating it!  
If you're using it from GitHub, consider starring the repository.

Enjoy Dynamic Worlds!
