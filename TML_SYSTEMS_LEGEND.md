# Terraria & tModLoader Systems Legend (Project Context)

Purpose: quick reference about Terraria/tModLoader systems this mod touches. Keep concise, add new entries as features grow.

## Core tModLoader types used
- **Mod** / `DynamicWorlds`: root mod class (inferred from project) providing content and systems.
- **ModItem** (`BiomeDowser`, `AnchoredTiles`, `ErasedTiles`, `StructureAnchorItem`, etc.): custom items with tooltips, right-click behavior, drag actions.
- **ModSystem** (`BiomeDowserSystem`, `BiomeDowserSettingsSystem`, `WorldRegenScheduler`, `RegenLoadingSystem`, `WorldProgress`): world-level hooks for loxading/saving, interface layers, regen scheduling, and global state.
- **ModPlayer** (`BiomeDowserPlayer`, player.cs): per-player state (drag selection, placement preferences, key/mouse handling, saved preferences).
- **ModCommand** (`BuildingZoneCommand`, `regenworldcommand`, `GiveToolsCommand`): chat commands for debugging/utility.
- **UIState / UIElement / UIPanel / UIList / UIText / UITextPanel / UIScrollbar**: tML UI framework for custom menus (Biome Dowser settings list/detail UI).
- **GameInterfaceLayer / LegacyGameInterfaceLayer**: injected layers for drawing custom UI (`BiomeDowserSettingsSystem.ModifyInterfaceLayers`). Mirrors vanilla settings layering: a UserInterface is updated in `ModSystem.UpdateUI` and drawn by inserting a Legacy layer before "Vanilla: Mouse Text".
- **Localization** (`en-US_Mods.DynamicWorlds.hjson`): string resources for tooltips and UI text.

## World/Tile systems
- **Teleport pylons (`TeleportPylonType`)**: vanilla pylon types; used to categorize zones and placement preferences.
- **Zone storage** (`BiomeDowserZone`, `BuildingZone`): captures rectangular regions with pylons, serialized via `TagCompound` during world save/load.
- **Tile scanning**: uses `WorldGen`, `SceneMetrics`, tile data to validate biome matches, check dungeon/temple, and find placement bounds.
- **Anchors/Structures**: integration with `StructureAnchorSystem` (no overlap with anchored tiles/structure zones).

## Input & interaction patterns
- **Mouse states** (`Main.mouseLeft`, `Main.mouseRight`, `mouseInterface`), **keyboard** (Shift): drag-select zones, Shift+Right-click opens settings, Shift+Click removes zone.
- **Overlay rendering** (`WorldToolOverlayHelper`): draws selection/zone overlays in `PostDrawTiles`.
- **Sound** (`SoundEngine`, `SoundID.Item4/Item8/Item14`): feedback on actions.

## Item behavior patterns
- **Tooltips** (`ModifyTooltips`): communicate usage, modes, current preference.
- **Right-click handling**: disabled for Biome Dowser mode cycling; still used in other tools (AnchoredTiles/ErasedTiles) for immediate actions.

## UI specifics (Biome Dowser settings)
- Two-column layout modeled after vanilla Settings: left column UIList of pylon types inside its own container + scrollbar; right column detail panel showing controls for placement mode, floating offset, ocean mode, sky island preference, aether preference.
- Navigation: list rows open detail; back button hides detail; Close button toggles UI off. Show/hide is done by append/remove (UIPanel has no `Hidden`).
- Colors/padding tuned to vanilla-like panels; sizes increased to avoid clipping and mirror Terraria settings proportions.

## Hooks & docs
- tModLoader UI pipeline: `UserInterface` updated via `ModSystem.UpdateUI` and drawn by inserting a `LegacyGameInterfaceLayer` in `ModSystem.ModifyInterfaceLayers` (see tML docs: https://docs.tmodloader.net/docs/stable/annotated.html for UIState/Interface layer patterns).
- Vanilla settings inspiration: Terraria’s in-game settings use a two-column list + detail pattern; this menu mimics that structure using tML UI primitives.

## Saving & persistence
- **World save**: zones serialized in `BiomeDowserSystem.SaveWorldData`; next ID tracked.
- **Player save**: per-player preferences saved in `BiomeDowserPlayer.SaveData/LoadData` (placement mode, floating offset, ocean/sky/aether flags).

## Build/runtime assumptions
- Built against tModLoader (Terraria 1.4+); `dotnet build` alone will miss Terraria/tML references—must build via tModLoader.
- Assets: uses HJSON localization, description files for Steam/Workshop, and PNG icons for UI/items.

## Quick interaction cheatsheet
- Drag with Biome Dowser: create pylon zone (must enclose exactly one vanilla pylon; no overlap with structure/anchored tiles).
- Shift+Click inside a Biome Dowser zone: remove it.
- Shift+Right-click while holding Biome Dowser: open settings UI.
- Preferences per pylon set in settings (surface/underground/floating, ocean mode, floating Y offset, sky island, aether cavern).

---
Append new systems/features here as they’re explored (e.g., CalamityCompat hooks, World regen scheduler details, NPC interactions).
