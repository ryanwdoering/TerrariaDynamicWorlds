# Building Zones - Refactored to World Ownership

## Architecture Change

### Before (Item-Based)
```
BuildingAnchorItem instances → Each tracked ZoneIds list
  ↓
Zones created by specific items and tied to them
Zones removed when item is lost or when player right-clicks item

Problem: Zones could be "lost" if the item was deleted/lost
```

### After (World-Based)
```
BuildingAnchorItem instances → Just tools (no zone tracking)
  ↓
BuildingAnchorSystem (ModSystem) ← OWNS all zones
  ↓
Zones persist automatically with world save/load
Zones are independent of any particular item
```

## Key Changes

### 1. **BuildingAnchorItem** - Simplified to a Tool
- ✅ **Removed:** `ZoneIds` list property
- ✅ **Removed:** Right-click handler (`RightClick()`)
- ✅ **Removed:** `SaveData()` and `LoadData()` (no zone tracking)
- ✅ **Changed:** Item is now stateless - purely a tool
- ✅ **Updated:** Tooltips to reflect world ownership

**Result:** Items can be deleted/dropped without affecting zones

### 2. **BuildingAnchorSystem** - Already Had World Persistence
- ✅ **Already had:** `SaveWorldData()` → Zones saved to world file
- ✅ **Already had:** `LoadWorldData()` → Zones loaded from world file
- ✅ **Now:** Definitive source of zone truth

**Result:** Zones automatically persist through world saves/loads

### 3. **BuildingAnchorPlayer** - Modified Zone Operations
- ✅ **Changed:** `CommitZone()` - No longer associates zones with items
- ✅ **Changed:** `RemoveZoneAtPosition()` - Removes from world, not from item list
- ✅ **Simplified:** Both methods work on global zone dictionary

### 4. **New Command: `/dwzone`**
Manage zones at the world level without needing items:

```
/dwzone list        - List all zones
/dwzone clear <id>  - Remove specific zone by ID
/dwzone clearall    - Remove all zones (careful!)
```

## Player Experience

### Zone Creation
```
BEFORE: "Building zone #1 added (1 total on this anchor)"
AFTER:  "Building zone #1 created: 3×5 (15 tiles)"
         (Zone belongs to the world, not the item)
```

### Zone Removal (Methods)

**Method 1: In-Game with Item**
- Hold Building Anchor
- Shift+Click on any zone
- Zone is removed from world immediately

**Method 2: Chat Command**
- `/dwzone list` - See all zones
- `/dwzone clear 1` - Remove zone #1
- `/dwzone clearall` - Remove everything

**Method 3: No Right-Click**
- Right-click no longer removes zones (item is now stateless)
- Use commands or Shift+Click instead

## World Persistence

### Save/Load Flow
```
Player saves world
    ↓
BuildingAnchorSystem.SaveWorldData() called
    ↓
All zones written to world.dat
    ↓

Player loads world
    ↓
BuildingAnchorSystem.LoadWorldData() called
    ↓
All zones restored from world.dat
    ↓
Zones exist regardless of item status
```

### Multiitem Safety
```
Item 1 (Building Anchor #1) → Creates Zone #1
Item 2 (Building Anchor #2) → Creates Zone #2
    ↓
Both zones saved to world independently
    ↓
If Item 1 is deleted:
  Zone #1 still exists in BuildingAnchorSystem.Zones
  Zone #1 can be removed via /dwzone or Shift+Click
```

## Benefits

1. **Durability** - Zones persist even if items are lost/deleted
2. **Flexibility** - Manage zones from chat commands without item
3. **Simplicity** - Items don't carry state; system owns everything
4. **Scalability** - Support unlimited zones without item complexity
5. **Consistency** - Single source of truth (BuildingAnchorSystem)

## Technical Details

### Data Storage
```csharp
// World-level persistence
BuildingAnchorSystem.Zones: Dictionary<int, BuildingZone>
  ↓ Saved to:
WorldData["BuildingZones"]: List<TagCompound>

// Item-level storage (REMOVED)
BuildingAnchorItem.ZoneIds: List<int>  ← DELETED
```

### Zone Lifecycle
```
1. CREATE: Player drags with Building Anchor
   → BuildingAnchorPlayer.CommitZone()
   → Zone added to BuildingAnchorSystem.Zones
   → Zone saved to world on next save

2. RESTORE: After world regen
   → BuildingAnchorSystem.RestoreAllZones()
   → All zones in dictionary are restored to world
   → Positions adjusted based on terrain changes

3. REMOVE: Shift+Click or /dwzone clear
   → Zone removed from BuildingAnchorSystem.Zones
   → Automatically reflected in next world save

4. LOAD: Player loads world
   → BuildingAnchorSystem.LoadWorldData()
   → All zones restored from world.dat
```

## Migration Notes

### For Existing Saves
- ✅ Old item `ZoneIds` data is no longer used
- ✅ Zones already in `BuildingAnchorSystem.Zones` persist
- ✅ No data loss - zones remain in world data
- ⚠️ Item-local zone lists are abandoned (acceptable)

### For New Saves
- ✅ Clean slate with zones owned by world
- ✅ No zone tracking on items
- ✅ Full command-line management available

## Future Enhancements
- [ ] Zone naming/description system
- [ ] Zone visualization improvements
- [ ] Batch zone operations
- [ ] Zone import/export
- [ ] Permissions system for multiplayer
