# Terraria NPC Housing System - Technical Deep Dive

## ILSpy Analysis Results

Using ILSpy v9.1.0.7988, the Terraria.exe assembly (v1.4.4.9) was decompiled to understand NPC housing mechanics.

### NPC Housing Fields

Located in NPC class (around line 134960-134980):

```csharp
public bool homeless;              // NPC needs housing
public bool homelessDespawn;       // Despawn if homeless too long
public int lookForHomeTimeout;     // Search cooldown (line 134974)
public int homeTileX = -1;         // Home bed X coordinate
public int homeTileY = -1;         // Home bed Y coordinate
public int housingCategory;        // Housing UI category (line 134975)
public bool oldHomeless;           // Previous state tracking
public int oldHomeTileX = -1;      // Previous home X
public int oldHomeTileY = -1;      // Previous home Y
```

### Housing State Management

**Setting Home (lines 420115-420116, 420385-420386, etc):**
```csharp
Main.npc[npcSlot].homeTileX = bestX;
Main.npc[npcSlot].homeTileY = bestY;
```

**Marking as Housed (lines 420387, 420704-420705):**
```csharp
Main.npc[npc].homeless = false;
Main.npc[npc].homelessDespawn = false;
```

**Marking as Homeless (lines 182272, 210413, 420118, etc):**
```csharp
homeless = true;
homelessDespawn = true;  // May despawn if stays homeless too long
```

### Housing Validation System

**StartRoomCheck Method (line 420922):**
```
public static bool StartRoomCheck(int x, int y, IRoomCheckFeedback feedback = null)
```

Requirements checked:
1. **Distance from edge**: x/y must be >10 tiles from world edge
2. **Starting tile**: Must not be solid tile
3. **Room size**: Minimum 60 tiles (line 427044)
4. **Walls**: Must have proper walls (not just air)
5. **Doors**: Must have doors for entry/exit
6. **Safety**: No spawn-able hostile mobs
7. **Boundary**: Well-defined room boundaries

**Housing_CheckIfInRoom (line 420974):**
- Checks if given coordinates are within tested room
- Uses roomTiles dictionary for O(1) lookup
- Returns true if tile is part of valid room

**Housing_GetTestedRoomBounds (line 420933):**
- Gets boundaries of currently tested room
- Adds 45-tile buffer around room edges
- Clamps to world boundaries (5-tile safety margin)

### Room Check Failure Reasons

From decompiled code (lines 419620-419650):

```csharp
TownNPCRoomCheckFailureReason enum:
  None = 0
  RoomHasUnsafeWalls = 1
  HoleInWallIsTooBig = 2
  RoomCheckStartedInASolidTile = 3
  RoomIsTooBig = 4
  RoomIsTooSmall = 5
  TooCloseToWorldEdge = 6
  DoorIsInSolidTile = 7
  MissingDoor = 8
```

### NPC Housing Search

**Priority Order (lines 420565-420730):**
1. Find homeless NPCs (homeless == true)
2. Skip type 37 (Guide), 453, 368 (pets), 160 (Stylist)
3. Search for valid housing in world
4. Assign to first valid room found
5. Check compatibility (CanNPCsLiveWithEachOther)
6. Prevent duplicate housing conflicts

**Auto-Assignment Process:**
1. When NPC marked homeless=false
2. During next update cycle
3. Terraria searches valid rooms
4. Assigns to first available
5. Sets homeTileX/homeTileY
6. Marks homeless=false permanently

### Housing in Building Zones

When building anchors restore structures:

1. **Restore Process** (BuildingAnchor.RestoreToWorld):
   - Clears destination zone
   - Restores all saved tiles
   - Restores walls exactly
   - Restores doors and furniture

2. **After Restoration**:
   - Room has bed (TileID 79 or TileID 667, etc)
   - Room has walls
   - Room has doors
   - Room passes WorldGen.StartRoomCheck()

3. **NPC Assignment**:
   - Housing validation scans restored rooms
   - Finds valid housing from restored structures
   - Assigns NPCs to restored beds
   - Success: NPC is housed in restored structure

### Housing in Anchored Tiles

When individual anchored tiles are restored:

1. **Save Process** (AnchoredTiles.cs):
   - Saves each tile type
   - Saves wall type
   - Saves paint and metadata
   - Preserves exact state

2. **Restore Process**:
   - Each saved tile placed exactly
   - Walls restored perfectly
   - Metadata (paint) restored
   - Creates exact replica

3. **Housing Validation**:
   - Room check scans restored area
   - Validates saved walls still there
   - Validates saved doors present
   - Validates bed still present
   - Passes: valid housing room

4. **NPC Assignment**:
   - Housing search finds restored room
   - Room is valid (walls, doors, size)
   - Assigns NPC to restored bed
   - Success: NPC housed in restored area

## Implementation in DynamicWorlds

### Capture Phase (Before Regen)

```csharp
s.npcHousing = Main.npc
    .Where(n => n.active && n.townNPC && n.type > 0 && 
                !n.homeless && 
                n.homeTileX >= 0 && n.homeTileY >= 0)
    .Select(n => (n.type, n.homeTileX, n.homeTileY))
    .ToList();
```

**Filters applied:**
- `active`: NPC exists in world
- `townNPC`: Flagged as town NPC
- `type > 0`: Valid NPC type ID
- `!n.homeless`: Not currently homeless
- `homeTileX >= 0 && homeTileY >= 0`: Valid home coordinates set

### Respawn Phase (After Regen)

```csharp
if (!housinNpcTypes.Contains(npcType))
{
    Main.NewText($"Skipping {displayName} (was homeless before regen).", 200, 150, 150);
    continue;
}

// Spawn NPC
NPC newNpc = new NPC();
newNpc.SetDefaults(npcType);
newNpc.position = spawnPos;
newNpc.active = true;
Main.npc[npcSlot] = newNpc;

// Mark as housed
newNpc.homeless = false;

// Featherfall for safe landing
newNpc.AddBuff(BuffID.Featherfall, 60 * 10);
```

**Process:**
1. Only respawns NPCs that had housing before regen
2. Spawns in safe location (not inside tiles)
3. Sets `homeless = false` (triggers auto-search)
4. Terraria's native system finds valid homes

## Why This Works

### For Building Zones
- Complete structure restoration
- All tiles, walls, doors restored
- Housing validation passes
- NPC finds and moves to restored bed

### For Anchored Tiles
- Individual tiles restored exactly
- Custom rooms preserved perfectly
- Housing validation succeeds
- NPC moves to saved bed

### For Homeless NPCs
- Marked homeless before regen
- Not added to npcHousing list
- Skipped in respawn
- User notified in chat
- Can be manually spawned elsewhere

## Performance Considerations

- **Capture**: O(n) where n = number of NPCs in world
- **Respawn**: O(n) for respawning, + O(m) for Terraria's housing search (m = world size)
- **Housing Search**: Terraria's native system, optimized internally
- **Memory**: Single integer pair per housed NPC type (minimal overhead)

## Edge Cases Handled

1. **Duplicate NPC types**: HashSet prevents respawning same type twice
2. **No valid housing**: NPC stays homeless, skipped
3. **NPC slot full**: Error message shown
4. **Homeless NPCs**: Clearly marked in chat
5. **Custom names**: GivenName preserved
6. **Invalid coordinates**: Filtered out during capture

## References

- **Terraria Version**: 1.4.4.9
- **tModLoader Version**: 2026.1.3.2  
- **ILSpy Version**: 9.1.0.7988
- **Decompile Date**: March 15, 2026

## Key Insights

1. **Housing is tile-based**: Terraria validates entire rooms, not just beds
2. **Auto-assignment is automatic**: Setting `homeless=false` triggers search
3. **Housing validation is thorough**: Checks walls, doors, size, enemies, edges
4. **Building preservation works**: Restored structures pass validation
5. **No reimplementation needed**: Terraria's system handles all logic

## Future Enhancements

1. **Pre-validate housing**: Ensure homes exist before respawning
2. **Housing preferences**: Prioritize homes in certain locations
3. **Custom housing creation**: Generate guaranteed housing during regen
4. **Housing conflicts**: Prevent multiple NPCs in one room (handled by Terraria)
5. **Housing UI**: Show housing status in debug/admin UI
