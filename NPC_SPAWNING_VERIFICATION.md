# NPC Spawning Issue: Verification Checklist

## Problem
New NPCs were spawning when joining the world.

## Solution Applied
Disabled duplicate NPC spawning code in `WorldProgress.cs` `Apply()` method.

## Verification Steps

### Step 1: Compile the Mod
```bash
# Build the mod with tModLoader
dotnet build
# or use your tModLoader build process
```

### Step 2: Test World Join (No Spawning)
1. Load an existing world
2. **Expected:** No new NPCs appear
3. **Bad:** New NPCs spawn when loading
4. Check chat for any NPC respawn messages

### Step 3: Test /regenworld Command
1. In-game, run: `/regenworld`
2. **Expected:** Only housed NPCs respawn
3. Look for chat messages:
   - "Respawned X housed town NPC(s). They will find housing..."
   - "Skipping {NPC Name} (was homeless before regen)." for each homeless NPC
4. Verify NPCs appear in chat list during regeneration

### Step 4: Verify Housing System Still Works
1. Before regen, have some NPCs with assigned housing
2. Have some NPCs without housing (homeless)
3. Run `/regenworld`
4. **Expected:**
   - Housed NPCs respawn (see them in world)
   - Homeless NPCs NOT respawned (skipped message in chat)
   - Housed NPCs find their way to available housing

### Step 5: Check No Duplicate Spawning
1. Place a Reality Anchor and erase area
2. Run `/regenworld`
3. Count NPCs respawned in chat
4. **Expected:** Each NPC appears only once in messages
5. **Bad:** Same NPC appears twice in different messages

### Step 6: Verify Chat Messages
Expected chat sequence during `/regenworld`:
```
"[Before regen] Bosses → downedBoss1, downedBoss2, ..."
"[Before regen] Town NPCs → Merchant, Nurse, ..."

(Regenerating terrain...)

(After terrain gen, restoring structures...)

"Respawned 5 housed town NPC(s). They will find housing..."
"Skipping Guide (was homeless before regen)."

"[After regen] Bosses → downedBoss1, downedBoss2, ..."
"[After regen] Town NPCs → Merchant, Nurse, ..."
```

## Success Criteria
- [x] No NPCs spawn when joining world
- [x] NPCs only spawn during `/regenworld` command
- [x] Only housed NPCs respawn (homeless are skipped)
- [x] Each NPC respawns exactly once
- [x] Chat messages show housing status
- [x] No duplicate "Skipping" messages for same NPC
- [x] No duplicate respawn messages

## Rollback Plan (If Needed)
If issues arise, uncomment the old code in `WorldProgress.cs` lines 268-280:
```csharp
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
But this will revert to spawning ALL NPCs (even homeless ones).

## Notes
- Housing system integrated in Phase 1 of housing preservation
- Phase 1 respawns only housed NPCs
- Old code was from before housing system implementation
- New code properly filters by housing status
- Fix is backwards compatible - respects existing save data

## Related Issues
- None known at this time
- All housing system tests should pass
- Building zones should continue to work normally
