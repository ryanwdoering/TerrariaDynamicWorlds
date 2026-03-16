# Dynamic Worlds

Dynamic Worlds is a single-player focused tModLoader utility mod for rebuilding a Terraria world's terrain without throwing away the playthrough built on top of it.

The mod snapshots your world state, saves the important things you marked, runs fresh world generation through a menu loading screen, restores your preserved data, and automatically loads you back into the regenerated world.

## What Dynamic Worlds Currently Does

- Runs `/regenworld [seed]` through a real loading screen instead of freezing the live game.
- Preserves major world progression such as Hardmode, ore tiers, boss flags, event flags, invasions, game mode, world identity, and saved NPC state.
- Preserves exact tiles with `Reality Anchor`.
- Forces empty space with `Reality Eraser`.
- Preserves full builds with `Structure Anchor`.
- Restores chest and dresser contents inside anchored tiles and structure zones.
- Reassigns town NPC housing when their homes survived through anchors or structure zones.
- Repairs preserved vanilla pylons so they register correctly again after regen.
- Automatically reloads the regenerated world and places the player at a valid spawn.
- Runs an automatic regen scheduler in single player, with a configurable interval in in-game days.

## Important Current Limits

- Single-player only. Multiplayer is not supported.
- The dedicated `Pylon Anchor` feature is currently parked and disabled in source.
- Vanilla pylons are repaired after regen if you preserved them, but the mod does not currently move a pylon to a matching biome automatically.
- `Preserve Dungeon Side` and `Preserve Biome Features` exist in config as planned options, but they are not implemented yet.
- Scheduled regen can now be enabled or disabled in config, and its day interval can be customized.

## Quick Start

1. Enter a world. Dynamic Worlds automatically gifts you the three core tools if you do not already have them.
2. Use `Reality Anchor` for exact tiles you never want to lose.
3. Use `Structure Anchor` for houses, bases, and large builds you want moved onto the new terrain as a unit.
4. Use `Reality Eraser` anywhere you always want cleared out after regen.
5. Run `/regenworld` when you are ready, or wait for the configured automatic cycle to trigger.

## How Regeneration Works

When regen starts, the flow is:

1. Capture world progression, NPC roster, housing data, player spawn context, anchored tiles, erased tiles, structure zones, and preserved container contents.
2. Save and quit the current world cleanly.
3. Switch to a menu-style loading screen.
4. Run normal Terraria/tModLoader world generation with a random seed, the current seed, or a seed you supplied.
5. Reapply preserved progression and config-aware world settings.
6. Clear erased tiles.
7. Restore structure zones.
8. Restore anchored tiles and preserved container contents.
9. Reassign preserved NPC housing where the restored room is still valid.
10. Re-register preserved vanilla pylons.
11. Reload the regenerated world automatically and place the player at bed spawn or world spawn.

The key practical detail is that regen does not continue inside live gameplay. The world is saved and exited first, then generation happens from the loading screen.

## What Gets Preserved

### World Progression

- Hardmode state
- World evil flag
- Game mode
- Boss progression
- Event and invasion progression
- Pre-Hardmode and Hardmode ore tiers
- World name, world ID, and seed metadata

### NPC and Housing State

- Town NPC roster, including mod town NPC type IDs
- NPC display names
- NPC position snapshots for respawn placement
- Saved housing assignments when the house survives through anchors or structure zones

### Player State Related to Regen

- Bed spawn when the bed survives
- Safe fallback to world spawn when the bed does not survive
- Post-load featherfall safety buffer
- Last-position save suppression during regen so the player is not pulled back to a stale spot

### Preserved World Data

- Exact anchored tiles, including walls, liquids, wires, slopes, and actuators
- Structure zones with full tile snapshots
- Chest and dresser contents from anchored tiles and structure zones
- Best-effort tile entity extra data on anchored tiles

## Mod Systems

These are the main runtime systems in the mod and what they are responsible for.

| System | File | Purpose |
| --- | --- | --- |
| `WorldProgressUtil` and `RoguelikeWorldSystem` | `DynamicWorlds/WorldProgress.cs` | Capture, save, load, and print the world progression snapshot. |
| `DynamicWorldRegenSystem` | `DynamicWorlds/RegenLoadingSystem.cs` | Handles the save-and-quit handoff, loading screen, background world generation, reload, and post-regen player placement. |
| `SingleplayerRegenHelper` | `DynamicWorlds/regenworldcommand.cs` | Builds the pending regen snapshot, applies preserved data back into the new world, and restores NPCs and housing. |
| `WorldRegenScheduler` | `DynamicWorlds/WorldRegenScheduler.cs` | Tracks the configured regen cycle, announces countdowns, and triggers automatic regen at midnight on regen day. |
| `AnchoredTileSystem` | `DynamicWorlds/AnchoredTiles.cs` | Stores anchored tiles, captures container contents, enforces anchor limits, and restores anchored data after regen. |
| `ErasedTileSystem` | `DynamicWorlds/ErasedTiles.cs` | Stores tiles marked for erasure and clears them before anchored and zoned content is restored. |
| `StructureAnchorSystem` | `DynamicWorlds/StructureAnchor.cs` | Stores structure zones, preserves full builds, translates them vertically to new ground, and keeps zone metadata up to date. |
| `PylonRestoreHelper` | `DynamicWorlds/PylonRestoreHelper.cs` | Recreates missing vanilla pylon tile entities after restore and refreshes the vanilla pylon system. |
| `DynamicWorldsPlayer` | `DynamicWorlds/player.cs` | Gifts tools, stores safe player position data, and handles post-regen player entry behavior. |
| `GuideGlobalNPC` | `DynamicWorlds/GuideDialogue.cs` | Adds a Guide dialogue page that explains the `Reality Anchor`. |

## World Tools

All three tools are auto-gifted on first world entry. You can also get them again with `/dwtools`.

Holding any one of the three tools shows all saved world overlays at once:

- anchored tiles
- erased tiles
- structure zones

### Reality Anchor

Use `Reality Anchor` when you want exact tiles to survive every regen.

How to use it:

- Left-click a tile to anchor or unanchor it.
- Click and drag to anchor or unanchor an entire rectangle.
- Right-click the item in your inventory to restore all anchored tiles immediately.

What it preserves:

- tiles
- walls
- liquids
- wires
- slopes and half blocks
- actuators
- container contents for anchored chests and dressers

Rules and limits:

- Tiles inside a structure zone cannot also be individually anchored.
- The anchor cap scales with progression from 5,000 to 100,000 tiles.
- Inventory right-click restore is gated by `Allow Cheats`.

Good use cases:

- beds
- wiring hubs
- farms with exact tile layouts
- chests with important items
- small handcrafted terrain details

### Reality Eraser

Use `Reality Eraser` when you want certain tiles to be empty space after every regen.

How to use it:

- Left-click a tile to mark or unmark it.
- Click and drag to mark or unmark a rectangle.
- Right-click the item in your inventory to clear all marked tiles immediately.

How it behaves:

- Erased tiles are cleared before anchored tiles are restored.
- If the same location is both erased and later restored by an anchor or structure zone, the preserved data wins because restoration happens afterward.

Rules and limits:

- The erasure cap scales from 50,000 to 1,000,000 tiles.
- Inventory right-click clear is gated by `Allow Cheats`.

Good use cases:

- clearing tunnels
- keeping arenas open
- forcing surface access points to stay empty
- clearing out naturally regenerated clutter near bases

### Structure Anchor

Use `Structure Anchor` when you want to preserve a full building or build area without anchoring every tile one by one.

How to use it:

- Click and drag to create a structure zone.
- Create as many separate structure zones as you want.
- Shift-click inside a structure zone to remove it.
- Use `/dwzone` and `/clearzones` for command-based zone management.

What it preserves:

- every tile inside the rectangle
- contained chest and dresser contents
- beds and housing geometry inside the zone
- surrounding build details included in the rectangle

How restoration works:

- The zone is restored as a unit.
- The build is translated vertically so its base settles onto the new ground at the same horizontal area.
- Small gaps under the restored footprint are bridged.
- Floating builds remain floating instead of getting a giant dirt pillar to the surface.

Rules:

- Structure zones cannot overlap other structure zones.
- Structure zones cannot overlap individually anchored tiles.

Good use cases:

- full houses
- village blocks
- hellevator entrance compounds
- sky builds
- biome outposts

## Commands

| Command | What it does | Notes |
| --- | --- | --- |
| `/regenworld [seed]` | Saves the world, opens the loading screen, runs regen, restores preserved data, and reloads the world. | Single-player only. Optional explicit seed. |
| `/snap` | Prints the current captured world progression snapshot. | Also prints the current scheduled regen status. |
| `/dwtools` | Gives the three world tools again. | Utility command. |
| `/dwinfo` | Prints counts and bounds for anchored tiles, erased tiles, and structure zones. | Useful for sanity checking saved data. |
| `/dwzone` | Lists structure zones. | Equivalent to `/dwzone list`. |
| `/dwzone list` | Lists all saved structure zones. | Shows size and coordinates. |
| `/dwzone clear <id>` | Removes one structure zone by ID. | Also accepts `/dwzone remove <id>`. |
| `/dwzone clearall` | Removes every structure zone. | Leaves anchors and erasures alone. |
| `/clearzones` | Removes all structure zones. | Single-player only. |
| `/hardmode [on|off]` | Forces Hardmode on or off. | Requires `Allow Cheats`. |
| `/down <bossOrEvent>` | Marks a boss or event as defeated. | Requires `Allow Cheats`. |
| `/killduplicatenpcs` | Removes duplicate town NPCs and keeps one of each type. | Cleanup tool. |

Supported `/down` targets:

```text
eye, evil, skeletron, queenbee, kingslime, deerclops,
mech1, mech2, mech3, plantera, golem, fishron, moonlord,
goblins, frost, pirates, martians, pumpkin, frostmoon
```

## Config Options

Dynamic Worlds currently uses the following server-side config values and planned config placeholders:

| Setting | What it means today | Notes |
| --- | --- | --- |
| `Enable Scheduled Regen` | Turns the automatic regen scheduler on or off. | When disabled, the saved day progress is paused instead of advancing. |
| `Scheduled Regen Interval (Days)` | Sets how many in-game days pass between scheduled automatic regens. | Clamped to at least 1 day. |
| `Allow Cheats` | Enables cheat-gated features such as `/hardmode`, `/down`, and inventory right-click actions on `Reality Anchor` and `Reality Eraser`. | Recommended off for normal play, on for testing/admin use. |
| `Preserve Evil Type` | Keeps Crimson/Corruption matching the previous world after regen. | Turn this off if you want regen to roll a new evil. |
| `Preserve Dungeon Side` | Planned setting only. | Documented for future work, not enforced yet, and may not appear in every config UI until that implementation is finished. |
| `Preserve Biome Features` | Planned setting only. | Documented for future work, not enforced yet, and may not appear in every config UI until that implementation is finished. |
| `Randomize World Each Regen` | Chooses whether each regen uses a fresh seed or reuses the current world seed when available. | If you want repeatable regens, turn this off or pass an explicit seed. |

## Pylons

Dynamic Worlds currently supports vanilla pylons in a limited but useful way:

- If a vanilla pylon is inside an anchored area or structure zone, the tile itself can be preserved.
- After regen, the mod recreates the missing vanilla pylon tile entity and refreshes Terraria's pylon system.
- This solves the common "the pylon tile is back but it no longer functions" problem.

What it does not currently do:

- It does not automatically move a pylon to a matching biome.
- It does not automatically convert a pylon to a different biome type.
- It does not currently provide special restore support for modded pylons.

So the practical answer is: a preserved vanilla pylon will work after regen if the restored location still satisfies Terraria's normal pylon rules. If the regenerated location is no longer appropriate for that pylon, the mod will not automatically swap it to a better biome right now.

## Compatibility

### Base Compatibility Expectations

Dynamic Worlds works best when:

- you stay in single player
- you keep the same mod list loaded before and after regen
- the other mods do not fundamentally break Terraria/tModLoader world generation or world reload flow

### Modded Tiles, Walls, Items, and NPCs

The mod is intentionally fairly friendly to content mods:

- Anchored tiles store tile IDs and wall IDs as `ushort`, so modded tiles and walls are part of the saved snapshot.
- Anchored and zoned container contents are serialized through tModLoader item data, so modded items inside chests and dressers are intended to survive.
- Town NPC snapshots include mod town NPC type IDs and names, so modded town NPCs can be respawned if the mod is still present.

Practical rule: if the same mod that added the item, tile, wall, or NPC is still installed after regen, Dynamic Worlds has a good chance of preserving it.

### Known Weaker Areas

Compatibility is weaker for:

- mods with unusual tile entities
- mods with custom pylons
- mods that depend on very specific worldgen placements
- mods that heavily rewrite worldgen flow or post-gen placement in ways Dynamic Worlds does not know about

Dynamic Worlds does generic best-effort tile entity extra-data restore for anchored tiles, but special-case repair currently only exists for vanilla pylons.

## FAQ

### Does regeneration force me out of the world, or does it happen while I keep playing?

Regen starts from in-game, but it does not continue in live gameplay. The mod saves and exits the world first, shows a loading screen, generates the new world there, then loads you back in automatically.

That means you are not walking around, mining, building, or breaking tiles while the actual regeneration is happening. This avoids the worst race-condition problems that a truly in-place live regen would create.

### Could moving around or breaking stuff during the process cause issues?

Once regen starts, the world is immediately saved and exited, so there is no live world to keep changing during generation. The bigger risks are not about movement during the load screen. They are:

- forgetting to anchor a critical tile or build
- assuming a pylon will automatically relocate to a valid biome
- expecting surrounding terrain to be rebuilt around a structure when only the structure itself was preserved

The command also refuses to start if a boss is alive.

### How does this work with pylons? Will it swap them to the appropriate biome?

Not yet. Today the mod only repairs preserved vanilla pylons so they function again if their tile entity was lost during restore.

It does not currently detect "this snow pylon should move to a snow biome" and it does not swap pylons to a more appropriate biome automatically. A separate `Pylon Anchor` feature was started in source, but it is currently disabled.

### Does this work with older worlds, including worlds that began in 1.3?

If the world can be loaded in your current Terraria/tModLoader setup, Dynamic Worlds can still snapshot the current playthrough state and regenerate from there.

What it cannot do is reconstruct an original unknown 1.3 seed or reproduce old 1.3 generation exactly. Regeneration uses the current Terraria/tModLoader world generation pipeline, not historical 1.3 terrain generation.

If you imported an old world and do not know the original seed:

- the mod can still regen the world
- the preserved builds, progression, NPCs, anchors, and zones can still carry forward
- exact repeatability is not guaranteed unless you pass an explicit seed yourself

If you want repeatable future regens on an old world, run `/regenworld <yourchosenseed>` with a seed you want to keep using.

### Can I use regen to replace my world evil?

Yes, with an important caveat.

If `Preserve Evil Type` is enabled, the regen result is forced back to the original world's evil. If you turn that setting off, the regenerated world keeps the evil chosen by the new worldgen result.

So you can use Dynamic Worlds to reroll from Corruption to Crimson or the other way around, but:

- it is not currently a direct "pick Crimson" toggle
- you may want to use a known seed if you want a specific outcome
- preserved anchored or zoned areas keep the blocks you preserved, so old evil blocks inside protected regions stay as captured

### Does Structure Anchor generate new terrain around a house so it ends up buried or naturally embedded?

Not in a fully procedural "wrap the whole house in new terrain" way.

What `Structure Anchor` does today:

- moves the structure vertically so it settles at the new ground level
- bridges small support gaps under the footprint
- avoids filling dirt all the way down under floating-island placements

What it does not do:

- generate a large custom hill around the build
- fully bury or encase the structure in newly created terrain unless those surrounding terrain tiles were part of the saved zone

If you want a house to remain embedded in dirt, stone, or another surrounding material, include that surrounding terrain in the structure zone or anchor it separately.

### Does it work with mods, and will Reality Anchor preserve modded items in chests?

In many cases, yes.

Reality Anchor and Structure Anchor both preserve captured chest and dresser contents, and the save format is intended to support modded items through tModLoader item serialization. As long as the same content mods are still installed, modded items in preserved containers have a good chance of surviving regen correctly.

The same general rule applies to modded tiles, walls, and town NPCs: they are much more likely to work if the supplying mod is still present and compatible after the regen.

The main caution points are modded tile entities, custom pylons, and major worldgen-overhaul mods.

## Snapshot Files

World progression snapshots are written next to the world file when possible:

```text
YourWorld.wld
YourWorld_progress.json
```

If that path is unavailable, the mod falls back to:

```text
tModLoader/DynamicWorlds/WorldProgress.json
```

## Build Notes

For local source builds, place the source under:

```text
tModLoader/ModSources/DynamicWorlds
```

Then build from Mod Sources inside tModLoader.

If you are building from this repo directly, be aware that local `dotnet build` can fail if your installed tModLoader runtime references do not match the target framework configured by the project.
