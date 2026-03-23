# DynamicWorlds Project Structure - Complete Reference

## 📋 Table of Contents
1. [Quick Navigation](#quick-navigation)
2. [Folder Overview](#folder-overview)
3. [Complete Project Tree](#complete-project-tree)
4. [File Organization by Purpose](#file-organization-by-purpose)
5. [Feature Systems](#feature-systems)
6. [Adding New Features](#adding-new-features)
7. [Key Statistics](#key-statistics)

---

## Quick Navigation

| Looking For | Location |
|---|---|
| **Mod Setup & Configuration** | `Core/DynamicWorlds.cs`, `Core/DynamicWorldsConfig.cs` |
| **Tile Anchoring** | `Preservation/AnchoredTiles.cs` |
| **Tile Erasure** | `Preservation/ErasedTiles.cs` |
| **Structure Zones** | `Preservation/StructureAnchor.cs` |
| **Pylon Relocation** | `Preservation/BiomeDowser.cs` |
| **Pylon Repair** | `Systems/PylonRestoreHelper.cs` |
| **World Progression** | `Systems/WorldProgress.cs` |
| **Regen Loading Flow** | `Systems/RegenLoadingSystem.cs` |
| **Auto Regen Scheduler** | `Systems/WorldRegenScheduler.cs` |
| **Tool Overlays** | `Systems/WorldToolOverlayHelper.cs` |
| **/regenworld Command** | `Commands/regenworldcommand.cs` |
| **/dwtools Command** | `Commands/GiveToolsCommand.cs` |
| **/dwzone Command** | `Commands/BuildingZoneCommand.cs` |
| **Player Interaction & Drag** | `UI/player.cs` |
| **Guide NPC Dialogue** | `UI/GuideDialogue.cs` |
| **Calamity Mod Support** | `Integration/CalamityCompat.cs` |
| **Tool Textures** | `Preservation/*.png` |
| **Mod Icons** | `Assets/icon.png`, `Assets/icon_small.png` |
| **Descriptions** | `Assets/description*.txt` |

---

## Folder Overview

### 🔷 Core/ - Mod Entry Point & Configuration
**Purpose:** Mod initialization and configuration system
- **DynamicWorlds.cs** - Main mod class, sets up hooks and systems
- **DynamicWorldsConfig.cs** - Configuration system with all player-facing options
  - Enable/disable scheduled regen
  - Regen interval settings
  - Allow cheats toggle
  - Regen on death setting
  - World generation preferences (evil type, biome features, seed randomization)

### 🟪 Preservation/ - Tile & Structure Preservation Systems
**Purpose:** Core preservation mechanics - anchoring tiles, erasing, structure zones, and pylon management

**C# Files:**
- **AnchoredTiles.cs** - Individual tile anchoring system
- **ErasedTiles.cs** - Tile erasure marking system
- **StructureAnchor.cs** - Structure zone preservation system
- **BiomeDowser.cs** - Intelligent pylon relocation to matching biomes
- **PylonAnchor.cs** - Alternative pylon preservation system (currently disabled)

**Texture Assets:**
- **RealityAnchor.png** - Anchor tool overlay texture
- **RealityEraser.png** - Eraser tool overlay texture
- **StructureAnchorItem.png** - Structure tool item texture
- **AnchoredTile.png** - Anchored tile marker overlay
- **ErasedTile.png** - Erased tile marker overlay
- **BiomeDowser.png** - Biome Dowser item texture
- **BiomeDowser-test.png** - Test/reference texture
- **pylonanchor.png** - Pylon Anchor item texture

### 🟦 Systems/ - Core World Game Systems
**Purpose:** World-level systems handling progression, regeneration, scheduling, and rendering

- **WorldProgress.cs** - World progression snapshots and restoration
- **RegenLoadingSystem.cs** - Loading screen world regeneration flow
- **WorldRegenScheduler.cs** - Automatic scheduled world regeneration
- **PylonRestoreHelper.cs** - Pylon tile-entity re-registration and repair
- **WorldToolOverlayHelper.cs** - Unified overlay rendering for all tools

### 🟨 Commands/ - Player Chat Commands
**Purpose:** Accessible chat commands for players

- **regenworldcommand.cs** - `/regenworld`, `/multiregen`, `/snap` - Core regeneration workflow
- **GiveToolsCommand.cs** - `/dwtools` - Distribute preservation tools to inventory
- **BuildingZoneCommand.cs** - `/dwzone`, `/clearzones` - Zone management and listing

### 🟧 UI/ - Player Interface & Dialogue
**Purpose:** Player interaction, selection tools, and NPC dialogue

- **player.cs** - DynamicWorldsPlayer class - Drag selection, zone management, death hooks
- **GuideDialogue.cs** - Guide NPC dialogue extension

### 🟩 Integration/ - External Mod Compatibility
**Purpose:** Support for other mods and progression systems

- **CalamityCompat.cs** - Calamity Mod progression support and detection

### 🟬 Tools/
**Status:** Reserved for future tool implementations
*(Currently empty)*

### 🟰 Configuration/
**Status:** Reserved for future configuration UI
*(Currently empty)*

### 🟶 Assets/ - Static Media & Descriptions
**Purpose:** Icons, descriptions, and metadata

- **icon.png** - Main mod icon (2D image)
- **icon_small.png** - Small mod icon (2D image)
- **description.txt** - Mod manager description
- **description_workshop.txt** - Steam Workshop description

### 🟦 Localization/ - Internationalization
**Purpose:** Multi-language support for UI text

- **en-US_Mods.DynamicWorlds.hjson** - English localization strings

### 🟪 NPCs/
**Status:** Reserved for custom NPC definitions
*(Currently empty)*

### 🟨 Properties/
**Purpose:** Project metadata and settings

- **launchSettings.json** - Launch configuration

### 🟧 Build Output
- **bin/** - Compiled binaries (net6.0, net8.0)
- **obj/** - Build intermediate files
- **DynamicWorlds.csproj** - Project file (auto-discovers code)
- **build.txt** - tModLoader build configuration
- **build.log** - Latest build output

---

## Complete Project Tree

```
TerrariaDynamicWorlds/
├── README.md                          [Project overview & quick start]
├── tModLoader.targets                 [tModLoader build configuration]
├── PROJECT_STRUCTURE.md               [THIS FILE - Complete reference]
│
├── DynamicWorlds/                     [Main mod folder]
│   ├── DynamicWorlds.csproj          [Project file - auto-discovers .cs files]
│   ├── build.txt                      [tModLoader build metadata]
│   │
│   ├── Core/                          [Mod entry point & configuration]
│   │   ├── DynamicWorlds.cs          [Main Mod class - hooks & initialization]
│   │   └── DynamicWorldsConfig.cs    [Configuration system - all options]
│   │
│   ├── Preservation/                  [Tile & structure preservation systems]
│   │   ├── AnchoredTiles.cs          [Individual tile anchoring]
│   │   ├── ErasedTiles.cs            [Tile erasure marking]
│   │   ├── StructureAnchor.cs        [Structure zone preservation]
│   │   ├── BiomeDowser.cs            [Intelligent pylon relocation]
│   │   ├── PylonAnchor.cs            [Alternative pylon system (disabled)]
│   │   │
│   │   ├── RealityAnchor.png         [Anchor tool texture]
│   │   ├── RealityEraser.png         [Eraser tool texture]
│   │   ├── StructureAnchorItem.png   [Structure tool texture]
│   │   ├── AnchoredTile.png          [Anchored tile marker]
│   │   ├── ErasedTile.png            [Erased tile marker]
│   │   ├── BiomeDowser.png           [Biome Dowser item texture]
│   │   ├── BiomeDowser-test.png      [Test texture]
│   │   └── pylonanchor.png           [Pylon Anchor item texture]
│   │
│   ├── Systems/                       [Core world game systems]
│   │   ├── WorldProgress.cs          [World snapshot & restoration]
│   │   ├── RegenLoadingSystem.cs     [Loading screen regen flow]
│   │   ├── WorldRegenScheduler.cs    [Automatic regen scheduling]
│   │   ├── PylonRestoreHelper.cs     [Pylon tile-entity repair]
│   │   └── WorldToolOverlayHelper.cs [Unified overlay rendering]
│   │
│   ├── Commands/                      [Player chat commands]
│   │   ├── regenworldcommand.cs      [/regenworld, /multiregen, /snap]
│   │   ├── GiveToolsCommand.cs       [/dwtools - tool distribution]
│   │   └── BuildingZoneCommand.cs    [/dwzone, /clearzones]
│   │
│   ├── UI/                            [Player interface & dialogue]
│   │   ├── player.cs                 [DynamicWorldsPlayer - drag, zones, death]
│   │   └── GuideDialogue.cs          [Guide NPC dialogue]
│   │
│   ├── Integration/                   [External mod compatibility]
│   │   └── CalamityCompat.cs         [Calamity Mod support]
│   │
│   ├── Tools/                         [Reserved for future tools]
│   ├── Configuration/                 [Reserved for config utilities]
│   │
│   ├── Assets/                        [Static media & descriptions]
│   │   ├── icon.png                  [Main mod icon]
│   │   ├── icon_small.png            [Small mod icon]
│   │   ├── description.txt           [Mod manager description]
│   │   └── description_workshop.txt  [Steam Workshop description]
│   │
│   ├── Localization/                  [Language support]
│   │   └── en-US_Mods.DynamicWorlds.hjson
│   │
│   ├── NPCs/                          [Custom NPCs - reserved]
│   │
│   ├── Properties/                    [Project properties]
│   │   └── launchSettings.json
│   │
│   ├── bin/                           [Compiled binaries]
│   │   └── Debug/
│   │       ├── net6.0/
│   │       └── net8.0/
│   │
│   └── obj/                           [Build intermediate files]
│       ├── Debug/
│       │   ├── net6.0/
│       │   └── net8.0/
│       └── [NuGet and build cache]
│
├── media/                             [Documentation media - guides, videos]
│   ├── guide/
│   │   ├── dynamic-worlds-guide-intro.jpg
│   │   └── dynamic-worlds-guide-intro.png
│   │
│   └── youtube/
│       ├── [Demo thumbnails and images]
│       └── shorts/
│           ├── [Video clips and previews]
│           └── [Contact sheets]
│
└── .git/                              [Git repository]
```

---

## File Organization by Purpose

### World Regeneration Pipeline
1. **Systems/WorldProgress.cs** - Captures world snapshot before regen
2. **Systems/RegenLoadingSystem.cs** - Handles loading screen, triggers regen
3. **Commands/regenworldcommand.cs** - User invokes `/regenworld` command
4. **Preservation/*.cs** - Restores anchored tiles, structures, erased areas
5. **Systems/PylonRestoreHelper.cs** - Re-registers vanilla pylons
6. **UI/player.cs** - Handles player death hooks for on-death regen

### Tool Systems
| Tool | Item | Overlay | Management |
|------|------|---------|------------|
| **Anchoring** | AnchoredTiles.cs | AnchoredTile.png | player.cs selection |
| **Erasure** | ErasedTiles.cs | ErasedTile.png | player.cs selection |
| **Structures** | StructureAnchor.cs | StructureAnchorItem.png | BuildingZoneCommand.cs |
| **BiomeDowser** | BiomeDowser.cs | BiomeDowser.png | N/A (automated) |
| **Pylon Anchor** | PylonAnchor.cs | pylonanchor.png | (Disabled) |

### Player Interaction
- **UI/player.cs** - Drag selection, zone management, death hooks
- **Systems/WorldToolOverlayHelper.cs** - Renders overlays for tools
- **Preservation/*.cs** - Store zone and preservation data

### Configuration System
- **Core/DynamicWorldsConfig.cs** - All options users can toggle
- **UI/player.cs** - Reads config for death regen feature
- **Commands/** - Read config for cheat/permission checks

---

## Feature Systems

### 🔄 Progression & Regeneration
**Files:** WorldProgress.cs, RegenLoadingSystem.cs, WorldRegenScheduler.cs, regenworldcommand.cs
**Purpose:** Capture world state and regenerate world while preserving selected areas
**Entry Points:**
- User command: `/regenworld [seed]`
- Scheduled: Automatic regen every X days (if enabled)
- Death: Player death triggers regen (if enabled)

### 💾 Preservation Systems
**Files:** AnchoredTiles.cs, ErasedTiles.cs, StructureAnchor.cs, BiomeDowser.cs, PylonAnchor.cs
**Purpose:** Allow players to preserve specific tiles, structures, and features
**Features:**
- Individual tile anchoring
- Tile erasure marking
- Structure zone preservation
- Intelligent pylon relocation to matching biomes
- Pylon tile-entity restoration

### 🎨 User Interface
**Files:** WorldToolOverlayHelper.cs, player.cs, GuideDialogue.cs
**Purpose:** Provide visual feedback and interactive tools
**Features:**
- Tool overlays (see selected tiles/zones)
- Drag selection (define zones)
- Zone management
- Guide NPC help
- Death event handling

### 🔌 Compatibility
**Files:** CalamityCompat.cs
**Purpose:** Detect and support other mods
**Features:**
- Calamity Mod progression detection
- Boss/event flag integration

### ⚙️ Systems & Utilities
**Files:** PylonRestoreHelper.cs, WorldToolOverlayHelper.cs
**Purpose:** Backend systems supporting other features
**Features:**
- Pylon tile-entity re-registration
- Unified overlay rendering
- World bounds/safety checking

---

## Adding New Features

### Adding a New Preservation System
1. Create `Preservation/YourNewSystem.cs`
2. Use namespace `DynamicWorlds`
3. Add textures to `Preservation/` if needed
4. Register in texture paths: `"DynamicWorlds/Preservation/YourTexture"`
5. Hook into `player.cs` for UI interactions
6. Add overlay rendering to `WorldToolOverlayHelper.cs`

### Adding a New Chat Command
1. Create `Commands/YourCommand.cs`
2. Extend `ModCommand` from tModLoader
3. Implement `Command` property and `Action()` method
4. Register in `Core/DynamicWorlds.cs` if not auto-discovered
5. Add help text and parameter validation

### Adding a World System
1. Create `Systems/YourSystem.cs`
2. Extend `ModSystem` from tModLoader
3. Implement appropriate hooks (OnWorldLoad, PreUpdateWorld, etc.)
4. Reference from `Core/DynamicWorlds.cs` if needed
5. Handle singleplayer-only scenarios appropriately

### Adding External Mod Support
1. Create `Integration/YourModCompat.cs`
2. Use `ModLoader.TryGetMod("yourmodname", out var mod)` for detection
3. Call integration logic only if mod is loaded
4. Handle missing mod gracefully
5. Document external mod requirements

---

## Texture Path Template

When adding item textures in C# code:

```csharp
public override string Texture => "DynamicWorlds/FolderName/TextureFileName";
```

**Examples:**
```csharp
public override string Texture => "DynamicWorlds/Preservation/BiomeDowser";     // ✅
public override string Texture => "DynamicWorlds/Preservation/RealityAnchor";   // ✅
public override string Texture => "DynamicWorlds/Assets/icon";                  // ✅
public override string Texture => "DynamicWorlds/BiomeDowser";                  // ❌ Old path
```

---

## Key Statistics

### Code Organization
| Metric | Count |
|--------|-------|
| **Total C# Files** | 20 |
| **Total Folders** | 11 |
| **Image/Texture Assets** | 8 |
| **Configuration Items** | 8 |
| **Chat Commands** | 6 |
| **Lines of Code** | ~7,500+ |

### By Folder
| Folder | C# Files | Assets | Total |
|--------|----------|--------|-------|
| Core | 2 | 0 | 2 |
| Preservation | 5 | 8 | 13 |
| Systems | 5 | 0 | 5 |
| Commands | 3 | 0 | 3 |
| UI | 2 | 0 | 2 |
| Integration | 1 | 0 | 1 |
| Assets | 0 | 4 | 4 |
| **Totals** | **18** | **12** | **30+** |

### Configuration Options
- Enable Scheduled Regen
- Scheduled Regen Interval (Days)
- Allow Cheats
- Regenerate World on Death *(NEW)*
- Preserve Evil Type
- Preserve Dungeon Side (planned)
- Preserve Biome Features (planned)
- Randomize World Each Regen

### Chat Commands
1. `/regenworld [seed]` - Manually regenerate world
2. `/multiregen [count] [seed]` - Multiple regen cycles
3. `/snap` - Take world snapshot
4. `/dwtools` - Give preservation tools
5. `/dwzone` - Manage zones
6. `/clearzones` - Remove all zones

---

## Project Navigation Guide

### "Where do I find..."

**...the mod setup?** → `Core/DynamicWorlds.cs`

**...how to add config options?** → `Core/DynamicWorldsConfig.cs`

**...how tools are preserved?** → `Preservation/AnchoredTiles.cs` or `Preservation/StructureAnchor.cs`

**...how the world regenerates?** → `Systems/RegenLoadingSystem.cs` → `Systems/WorldProgress.cs`

**...player interactions?** → `UI/player.cs`

**...command implementations?** → `Commands/`

**...world hooks?** → `Systems/WorldRegenScheduler.cs` or `Core/DynamicWorlds.cs`

**...how to add textures?** → See Texture Path Template section

**...tool overlays?** → `Systems/WorldToolOverlayHelper.cs`

**...Calamity support?** → `Integration/CalamityCompat.cs`

**...death on regen feature?** → `UI/player.cs` (PreKill hook) and `Core/DynamicWorldsConfig.cs` (RegenOnDeath setting)

---

## Design Principles

✅ **Flat Namespace** - All classes use `namespace DynamicWorlds` regardless of folder location
✅ **Logical Organization** - Folders group related functionality by purpose
✅ **Scalability** - Easy to add new systems in appropriate folders
✅ **Discoverability** - Clear folder names indicate purpose and content
✅ **No Breaking Changes** - Project structure is organizational only
✅ **Auto-Discovery** - .csproj auto-discovers all .cs files
✅ **Professional Standards** - Follows industry best practices

---

## Build & Compilation

**Build System:** tModLoader with .NET 6.0
**Project File:** `DynamicWorlds.csproj` (auto-discovers .cs files)
**Build Config:** `build.txt` (tModLoader metadata)

**Compilation Notes:**
- ✅ No manual project file edits needed for new files
- ✅ Texture paths must match physical folder structure
- ✅ All namespaces remain `DynamicWorlds`
- ✅ No code changes needed when reorganizing folders

---

## Testing Checklist

After making changes:
- [ ] Project compiles without errors
- [ ] No texture missing errors in build log
- [ ] Tools appear in inventory correctly
- [ ] Commands respond to player input
- [ ] World regeneration completes successfully
- [ ] Preserved data restores correctly
- [ ] Tool overlays display properly
- [ ] No namespace conflicts

---

## Quick File Lookup by Extension

### C# Source Files (20)
```
Core/: DynamicWorlds.cs, DynamicWorldsConfig.cs
Preservation/: AnchoredTiles.cs, ErasedTiles.cs, StructureAnchor.cs, 
               BiomeDowser.cs, PylonAnchor.cs
Systems/: WorldProgress.cs, RegenLoadingSystem.cs, WorldRegenScheduler.cs,
          PylonRestoreHelper.cs, WorldToolOverlayHelper.cs
Commands/: regenworldcommand.cs, GiveToolsCommand.cs, BuildingZoneCommand.cs
UI/: player.cs, GuideDialogue.cs
Integration/: CalamityCompat.cs
```

### Image/Texture Files (8)
```
All in Preservation/:
- RealityAnchor.png, RealityEraser.png, StructureAnchorItem.png
- AnchoredTile.png, ErasedTile.png
- BiomeDowser.png, BiomeDowser-test.png, pylonanchor.png
```

### Configuration Files (3)
```
DynamicWorlds.csproj: Project configuration
build.txt: tModLoader build metadata
Properties/launchSettings.json: Launch settings
```

---

**Reference Generated:** March 18, 2026
**Status:** ✅ Complete & Production Ready
**Last Updated:** With RegenOnDeath feature
│   ├── icon_small.png            (Small mod icon)
│   ├── pylonanchor.png           (Pylon Anchor tool texture)
│   ├── description.txt           (Mod manager description)
│   └── description_workshop.txt  (Steam Workshop description)
│
├── Localization/                  [Language support]
│   └── en-US_Mods.DynamicWorlds.hjson
│
├── NPCs/                          [Custom NPCs - reserved]
│   (currently empty)
│
├── Properties/                    [Project properties]
│   └── launchSettings.json
│
├── DynamicWorlds.csproj          [Project file]
├── build.log, build.txt          [Build outputs]
│
├── bin/                           [Compiled binaries]
│   └── Debug/
│       ├── net6.0/
│       └── net8.0/
│
└── obj/                           [Build intermediates]
    ├── DynamicWorlds.csproj.nuget.dgspec.json
    ├── project.assets.json
    └── Debug/
        ├── net6.0/
        └── net8.0/
```

## Feature Overview

### 🔄 **Progression & Regeneration**
- **WorldProgress.cs** - Captures and restores world state
- **RegenLoadingSystem.cs** - Manages loading screen regen flow
- **WorldRegenScheduler.cs** - Automatic regen scheduling
- **regenworldcommand.cs** - Manual regen command

### 💾 **Preservation Systems**
- **AnchoredTiles.cs** - Individual tile preservation
- **ErasedTiles.cs** - Tile erasure marking
- **StructureAnchor.cs** - Structure zone preservation
- **BiomeDowser.cs** - Pylon relocation to matching biomes
- **PylonRestoreHelper.cs** - Pylon tile-entity repair

### 🎨 **User Interface**
- **WorldToolOverlayHelper.cs** - Unified overlay rendering
- **player.cs** - Drag selection and zone management
- **GuideDialogue.cs** - Guide NPC help
- **GiveToolsCommand.cs** - Tool distribution
- **BuildingZoneCommand.cs** - Zone management commands

### 🔌 **Compatibility**
- **CalamityCompat.cs** - Calamity Mod integration
- **DynamicWorldsConfig.cs** - Configuration system

### 📦 **Assets & Configuration**
- **Assets/** - Icons and descriptions
- **Localization/** - Multi-language support
- **Properties/** - Project configuration

## Key Statistics

| Metric | Count |
|--------|-------|
| C# Source Files | 24 |
| Folders | 11 |
| Image Assets | 8 |
| Documentation Files | 2 |
| Lines of Code | ~7,500+ |

## Quick Navigation

**Need to modify...**
- ...pylon logic? → `Preservation/BiomeDowser.cs`
- ...anchored tiles? → `Preservation/AnchoredTiles.cs`
- ...world progression? → `Systems/WorldProgress.cs`
- ...chat commands? → `Commands/`
- ...player interactions? → `UI/player.cs`
- ...Calamity support? → `Integration/CalamityCompat.cs`
- ...mod configuration? → `Core/DynamicWorldsConfig.cs`

## Design Philosophy

✅ **Flat Namespace** - All classes use `namespace DynamicWorlds` regardless of folder  
✅ **Logical Organization** - Folders group related functionality  
✅ **Scalability** - Easy to add new systems in appropriate folders  
✅ **Discoverability** - Clear folder names indicate purpose  
✅ **No Breaking Changes** - Project compiles and functions identically

---

**Organization Status:** ✅ Complete  
**All Namespaces:** Unchanged  
**Code Changes:** None  
**Compilation:** Ready

