# Dynamic Worlds
### Persistent World Progression + Regenerating Worlds for Terraria (tModLoader)

Dynamic Worlds  allows players to regenerate their world's terrain without losing their game progression. The mod stores world state — including bosses defeated, Hardmode, ore tiers, and invasion progress — in a JSON snapshot file that persists across regenerations.

---

## ✨ Features

### 🔁 World Regeneration with Progress Persistence
Regenerate your world's layout at any time using:
```
/regenworld
```
Terrain is rebuilt using a new random seed, while your progression is restored instantly.

### ⏳ Automatic World Regeneration Scheduler
The world automatically regenerates every **7 in-game days**. A spooky countdown builds tension as the day approaches:
- **3 days out** — *"☠ The ground trembles. The world will regenerate in 3 days... ☠"*
- **1 day out** — *"☠ Something wicked approaches. The world regenerates TOMORROW. ☠"*
- **Dawn of regen day** — *"☠ The world stirs... regeneration begins at midnight. ☠"*
- **Midnight** — the world tears itself apart and is reborn.

The day counter persists across game sessions via world save data. Use `/snap` to check how many days remain.

### 🧱 Reality Anchor — Persistent Structures
Mark any tile as *anchored* so it survives world regenerations intact:
- **Left-click** any tile while holding the **Reality Anchor** item to anchor or unanchor it. Anchored tiles glow with a subtle overlay.
- **Chests and dressers** remember their exact contents after regen.
- **Right-click** the Reality Anchor in your inventory to manually restore all anchored tiles at any time, without running a full regen.
- Anchor data is saved per-world and persists across sessions.

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
- World identity (`worldName`, `worldID`, `worldSeed`)

---

## 📜 Commands

| Command | Description |
|---------|-------------|
| `/regenworld` | Regenerates world terrain and reapplies all progression |
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

Regeneration steps:
1. Capture progression snapshot
2. Snapshot all anchored tile data and chest contents
3. Clear world
4. Reroll world using vanilla worldgen
5. Restore snapshot (bosses, Hardmode, ores, etc.)
6. Restore all anchored tiles and chest contents
7. Report differences before/after
8. Teleport player to new spawn

### Key Systems

| File | Purpose |
|------|---------|
| `WorldProgress.cs` | Snapshot capture, apply, and JSON persistence |
| `AnchoredTiles.cs` | Reality Anchor item, tile overlay, chest snapshot & restore |
| `WorldRegenScheduler.cs` | 7-day auto-regen timer with spooky countdown broadcasts |
| `GuideDialogue.cs` | Guide NPC Reality Anchor info page |
| `regenworldcommand.cs` | All chat commands (`/regenworld`, `/snap`, `/hardmode`, `/down`) |

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
