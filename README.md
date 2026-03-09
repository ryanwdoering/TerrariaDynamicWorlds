# Dynamic Worlds
### Persistent World Progression + Regenerating Worlds for Terraria (tModLoader)

Dynamic Worlds allows players to regenerate their world's terrain without losing their game progression. The mod stores world state — including bosses defeated, Hardmode, ore tiers, and invasion progress — in a JSON snapshot file that persists across regenerations.

---

## ✨ Features

### 🔁 World Regeneration with Progress Persistence
Regenerate your world's layout at any time using:
```
/regenworld [seed]
```
Terrain is rebuilt using a new random seed (or a seed you supply), while your progression is restored instantly. After teleporting to spawn, you receive **10 seconds of Featherfall** so you land safely.

### ⏳ Automatic World Regeneration Scheduler
The world automatically regenerates every **7 in-game days**. A spooky countdown builds tension as the day approaches:
- **3 days out** — *"☠ The ground trembles. The world will regenerate in 3 days... ☠"*
- **1 day out** — *"☠ Something wicked approaches. The world regenerates TOMORROW. ☠"*
- **Dawn of regen day** — *"☠ The world stirs... regeneration begins at midnight. ☠"*
- **Midnight** — the world tears itself apart and is reborn.

The day counter persists across game sessions via world save data. Use `/snap` to check how many days remain.

### 🧱 Reality Anchor — Persistent Structures
Mark any tile as *anchored* so it survives world regenerations intact. You receive the Reality Anchor automatically when you first enter a world.

- **Left-click** any tile while holding the Reality Anchor to anchor or unanchor it. Anchored tiles glow with a subtle overlay while the item is held.
- **Click and drag** to anchor or unanchor a rectangular area at once.
- **Chests and dressers** remember their exact contents after regen — items included.
- **Anchor your bed** to preserve your personal spawn point across regenerations. The mod validates the bed tile survived before reasserting your spawn.
- **Right-click** the Reality Anchor in your inventory to manually restore all anchored tiles immediately, without running a full regen. The item is never consumed.
- The anchor limit scales with boss progression — from **5,000** tiles at the start up to **100,000** after Moon Lord.
- The tooltip dynamically shows your current count, cap, fill color, and whether your bed is anchored.
- Anchor data is saved per-world and persists across sessions.

### 🧹 Reality Eraser — Guaranteed Empty Tiles
Mark any tile to be **cleared to empty space** after every regeneration. You receive the Reality Eraser automatically alongside the Reality Anchor.

- **Left-click** any tile while holding the Reality Eraser to mark or unmark it for erasure.
- **Click and drag** to mark or unmark a rectangular area at once.
- Erasure runs **before** anchored tiles are restored — so if a position is both anchored and erased, the anchor always wins.
- **Right-click** the Reality Eraser in your inventory to immediately clear all marked tiles now. The item is never consumed.
- The erasure limit is **much higher** than the anchor limit — from **50,000** tiles at the start up to **1,000,000** after Moon Lord.
- The tooltip dynamically shows your current count and cap.

### 💬 Guide NPC Dialogue
Talk to the **Guide** NPC for an in-world tutorial on the Reality Anchor. Click the **Crafting** button during Guide chat to open the Reality Anchor info page — the Guide will walk you through anchoring, chest persistence, and manual restore.

### 🧠 Snapshot System
The mod tracks and restores:
- Hardmode status
- World evil type (Crimson/Corruption)
- Game Mode (Classic/Expert/Master/Journey)
- Boss & event progression
- Ore tiers (both pre-Hardmode and Hardmode)
- Invasion completions
- Town NPC roster (NPCs present before regen are respawned after)
- World identity (`worldName`, `worldID`, `worldSeed`)

### 🏠 Player Position Persistence
Your last position in the world is saved when you exit and restored when you return, so you always pick up exactly where you left off.

---

## 📜 Commands

| Command | Description |
|---------|-------------|
| `/regenworld [seed]` | Regenerates world terrain and reapplies all progression. Optional seed can be a number or any string. |
| `/snap` | Prints the current world progression snapshot + days until next auto-regen |
| `/hardmode on/off` | Force toggles Hardmode |
| `/down <boss/event>` | Marks a boss or event as defeated |

### `/down` targets
`eye`, `evil`, `skeletron`, `queenbee`, `kingslime`, `deerclops`, `mech1`, `mech2`, `mech3`, `plantera`, `golem`, `fishron`, `moonlord`, `goblins`, `frost`, `pirates`, `martians`, `pumpkin`, `frostmoon`

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

Regeneration order:
1. Capture progression snapshot (bosses, NPCs, etc.)
2. Snapshot all anchored tile data and chest contents
3. Clear world
4. Reroll world using vanilla worldgen
5. Restore snapshot (bosses, Hardmode, ores, NPCs, etc.)
6. **Clear all erased tile positions**
7. Restore all anchored tiles and chest contents
8. Validate and reassert player bed spawn
9. Teleport player to spawn with 10 seconds of Featherfall
10. Report differences before/after

### Key Systems

| File | Purpose |
|------|---------|
| `WorldProgress.cs` | Snapshot capture, apply, NPC tracking, and JSON persistence |
| `AnchoredTiles.cs` | Reality Anchor item, tile overlay, drag selection, chest snapshot & restore |
| `ErasedTiles.cs` | Reality Eraser item, tile overlay, drag selection, erasure on regen |
| `WorldRegenScheduler.cs` | 7-day auto-regen timer with spooky countdown broadcasts |
| `GuideDialogue.cs` | Guide NPC Reality Anchor info page |
| `regenworldcommand.cs` | All chat commands (`/regenworld`, `/snap`, `/hardmode`, `/down`) |
| `player.cs` | Player position save/restore, Reality Anchor & Eraser gifting on first entry |

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

---

## ❤️ Support
If you're using this mod from Steam, consider rating it!
If you're using it from GitHub, consider starring the repository.

Enjoy Dynamic Worlds!
