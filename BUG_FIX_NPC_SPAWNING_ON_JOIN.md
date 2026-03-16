# Bug Fix: NPCs Spawning When Joining World

## Issue
**New NPCs were spawning every time the player joined the world,** instead of only during `/regenworld` command execution.

## Root Cause
**Duplicate NPC spawning code** in the world progression system:

### Two Methods Spawning NPCs
1. **Old method** in `WorldProgress.cs` `Apply()` (lines 269-280):
   ```csharp
   // OLD CODE - Spawned ALL NPCs unconditionally
   if (s.collectedNpcTypes != null)
   {
       foreach (int npcType in s.collectedNpcTypes)
       {
           bool alreadyPresent = Main.npc.Any(n => n.active && n.type == npcType);
           if (!alreadyPresent)
               NPC.SpawnOnPlayer(Main.myPlayer, npcType);
       }
   }
   ```

2. **New method** in `regenworldcommand.cs` `RespawnTownNPCsAtOriginalPositions()`:
   ```csharp
   // NEW CODE - Only respawns housed NPCs with proper housing logic
   private static void RespawnTownNPCsAtOriginalPositions(WorldProgressSnapshot before)
   {
       // ... housing-aware respawning ...
   }
   ```

### Execution Flow During Regen
```
/regenworld command called
    ↓
WorldProgressUtil.Apply(before)              // Line 95 in regenworldcommand.cs
    └─ Spawns ALL NPCs (OLD code)
    ↓
RespawnTownNPCsAtOriginalPositions(before)  // Line 163 in regenworldcommand.cs
    └─ Spawns housed NPCs (NEW code)
    ↓
RESULT: NPCs spawned TWICE!
```

## The Fix
**Disable the old NPC spawning code** in `WorldProgress.cs` `Apply()` method and add a comment explaining why:

```csharp
// NOTE: NPC respawning is now handled separately by RespawnTownNPCsAtOriginalPositions()
// in regenworldcommand.cs, which only respawns housed NPCs and respects housing assignments.
// Do NOT spawn NPCs here - it would duplicate respawning and override housing logic.
// See: regenworldcommand.cs line 163

// Commented out old code:
// if (s.collectedNpcTypes != null)
// {
//     foreach (int npcType in s.collectedNpcTypes)
//     {
//         bool alreadyPresent = Main.npc.Any(n => n.active && n.type == npcType);
//         if (!alreadyPresent)
//             NPC.SpawnOnPlayer(Main.myPlayer, npcType);
//     }
// }
```

## Files Modified
- **`WorldProgress.cs`** - Disabled old NPC spawning code (lines 268-280), added explanatory comment

## Testing
To verify the fix works:
1. Compile the mod
2. Load a world
3. Check that **no new NPCs spawn** when joining
4. Run `/regenworld` command
5. Verify that **only housed NPCs are respawned** (NPCs without housing should be skipped)
6. Check that chat shows: "Skipping {name} (was homeless before regen)." for homeless NPCs

## Related Code
- **New respawning logic:** `regenworldcommand.cs` lines 182-260 (RespawnTownNPCsAtOriginalPositions)
- **Housing capture:** `WorldProgress.cs` lines 191-196 (housing data collection)
- **Old spawning code:** `WorldProgress.cs` lines 268-280 (now disabled)

## Impact
- ✅ Fixes unwanted NPC spawning on world join
- ✅ Preserves new housing-aware respawning system
- ✅ NPCs only respawn during `/regenworld` command (not on join)
- ✅ Only housed NPCs are respawned (homeless are skipped)
- ✅ Housing assignments are properly preserved
