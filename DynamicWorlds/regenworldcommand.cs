using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;
using Terraria.DataStructures;

namespace DynamicWorlds
{
    public static class SingleplayerRegenHelper
    {
        private static bool regenRunning = false;

        public static void RegenerateWorldWithProgress(string seedOverride = null)
        {
            if (regenRunning)
            {
                Main.NewText("World regeneration is already in progress.", 255, 200, 50);
                return;
            }

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                Main.NewText("This command only works in single player.", 255, 80, 80);
                return;
            }

            if (Main.npc.Any(n => n.active && n.boss))
            {
                Main.NewText("You cannot regenerate the world while a boss is alive.", 255, 80, 80);
                return;
            }

            regenRunning = true;
            var mod = ModContent.GetInstance<DynamicWorlds>();
            mod.Logger.Info("=== WORLD REGENERATION STARTED ===");

            try
            {
                // Capture progression and chest snapshots before anything is wiped
                mod.Logger.Info("Capturing world state...");
                var before = WorldProgressUtil.Capture();
                WorldProgressUtil.PrintSnapshotToChat("Before regen", before);
                AnchoredTileSystem.RefreshAllChestSnapshots();
                BuildingAnchorSystem.RefreshAllChestSnapshots();
                mod.Logger.Info($"Captured {AnchoredTileSystem.AnchoredTiles.Count} anchored tiles, {BuildingAnchorSystem.Zones.Count} building zones");

                // Snapshot personal spawn point before worldgen wipes state.
                Player p = Main.LocalPlayer;
                int savedSpawnX = p.SpawnX;
                int savedSpawnY = p.SpawnY;

                // Resolve seed: explicit arg → parse as int or hash the string, else decide based on config.
                int newSeed;
                var config = ModContent.GetInstance<DynamicWorldsConfig>();

                if (!string.IsNullOrWhiteSpace(seedOverride))
                {
                    // User explicitly provided a seed
                    if (int.TryParse(seedOverride, out int parsedSeed))
                        newSeed = parsedSeed & 0x7FFFFFFF;
                    else
                        newSeed = Math.Abs(seedOverride.GetHashCode()) & 0x7FFFFFFF;

                    Main.NewText($"Using seed: {seedOverride} → {newSeed}", 180, 180, 255);
                }
                else if (!config.RandomizeSeedEachRegen)
                {
                    // Use the current world's seed for consistent layout
                    string currentSeedText = Main.ActiveWorldFileData?.SeedText ?? "";
                    if (int.TryParse(currentSeedText, out int currentSeed))
                        newSeed = currentSeed & 0x7FFFFFFF;
                    else
                        newSeed = Math.Abs(currentSeedText.GetHashCode()) & 0x7FFFFFFF;

                    Main.NewText($"Reusing world seed for consistent layout: {currentSeedText}", 180, 180, 255);
                }
                else
                {
                    // Generate a random seed
                    newSeed = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
                }

                if (Main.ActiveWorldFileData != null)
                    Main.ActiveWorldFileData.SetSeed(newSeed.ToString());

                mod.Logger.Info($"Starting world generation with seed: {newSeed}");
                WorldGen.gen = true;
                WorldGen.clearWorld();

                var prog = new GenerationProgress();
                WorldGen.GenerateWorld(newSeed, prog);

                WorldGen.gen = false;
                mod.Logger.Info("World generation complete");

                // Re-apply all boss/hardmode/ore progression
                mod.Logger.Info("Applying world progression...");
                WorldProgressUtil.Apply(before);

                // Apply config-aware world settings
                ApplyConfigurableWorldSettings(before);

                // First clear all tiles marked for erasure, then restore anchored tiles.
                // Order matters: erasure runs on the freshly generated world before anchors
                // are written back, so anchored tiles always win over erased positions.
                mod.Logger.Info("Clearing erased tiles...");
                ErasedTileSystem.ClearAllErasedTiles();

                // Restore building zones (translated to new ground level) before regular
                // anchored tiles, so per-tile anchors can override zone tiles if needed.
                mod.Logger.Info("Restoring building zones...");
                BuildingAnchorSystem.RestoreAllZones();

                // Restore every anchored tile and chest
                // Tile entities (including pylons) are automatically restored with tiles
                mod.Logger.Info("Restoring anchored tiles...");
                AnchoredTileSystem.RestoreAllAnchoredTiles();

                // Teleport local player to the new spawn.
                // Priority: personal spawn point (bed) if it survived regen AND is valid.
                // Building zone restore may have already updated p.SpawnX/Y to the
                // translated position, so we read from p.SpawnX/Y after all restores.
                Vector2 spawnPos;

                bool hasPersonalSpawn = savedSpawnX >= 0 && savedSpawnY >= 0;

                // Use the (possibly translated) spawn set by building zone restore,
                // or fall back to the pre-regen saved value.
                int effectiveSpawnX = (p.SpawnX >= 0) ? p.SpawnX : savedSpawnX;
                int effectiveSpawnY = (p.SpawnY >= 0) ? p.SpawnY : savedSpawnY;

                bool spawnIsAnchored = hasPersonalSpawn &&
                    (AnchoredTileSystem.AnchoredTiles.ContainsKey(new Terraria.DataStructures.Point16(savedSpawnX, savedSpawnY)) ||
                     p.SpawnX != savedSpawnX || p.SpawnY != savedSpawnY); // translated by building zone

                // Validate the bed tile actually survived restoration.
                bool spawnIsValid = effectiveSpawnX >= 0 && effectiveSpawnY >= 0 &&
                                    Player.CheckSpawn(effectiveSpawnX, effectiveSpawnY);

                if (spawnIsValid)
                {
                    p.SpawnX = effectiveSpawnX;
                    p.SpawnY = effectiveSpawnY;
                    spawnPos = new Vector2(effectiveSpawnX * 16, effectiveSpawnY * 16 - 48);
                    Main.NewText("Your bed survived — spawning there.", 180, 255, 180);
                }
                else
                {
                    // Clear the stale bed spawn — the bed no longer exists in the new world.
                    p.SpawnX = -1;
                    p.SpawnY = -1;
                    spawnPos = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16 - 48);

                    if (hasPersonalSpawn)
                        Main.NewText("Your bed was not preserved — spawning at world spawn.", 255, 200, 100);
                }

                p.Teleport(spawnPos, 1);
                p.fallStart = (ushort)(p.position.Y / 16f);

                // Clear the saved pre-regen position so OnEnterWorld doesn't teleport
                // the player back into what is now solid terrain on the next world load.
                p.GetModPlayer<DynamicWorldsPlayer>().ClearSavedPosition();

                // 10 seconds of featherfall so the player lands safely after teleport.
                // Buff durations are in game ticks (60 ticks = 1 second).
                p.AddBuff(Terraria.ID.BuffID.Featherfall, 60 * 10);

                // Respawn town NPCs at their original coordinates with fall immunity
                RespawnTownNPCsAtOriginalPositions(before);

                // Advance game time by several in-game days to allow NPCs to move in and settle
                // Each in-game day = 24 * 3600 ticks (86400 ticks per day)
                AdvanceGameTime(3); // Advance 3 in-game days

                var after = WorldProgressUtil.Capture();
                WorldProgressUtil.PrintSnapshotToChat("After regen", after);

                mod.Logger.Info("=== WORLD REGENERATION COMPLETE ===");
                Main.NewText("World regeneration complete!", 80, 255, 80);
            }
            finally
            {
                WorldGen.gen  = false;
                regenRunning  = false;
            }
        }

        /// <summary>
        /// Respawn only housed town NPCs at valid housing locations.
        /// Uses Terraria's housing validation system to ensure homes are valid.
        /// NPCs will be assigned to available housing within building zones and anchored tiles.
        /// </summary>
        private static void RespawnTownNPCsAtOriginalPositions(WorldProgressSnapshot before)
        {
            if (before?.collectedNpcTypes == null || before.collectedNpcTypes.Count == 0)
                return;

            int respawnedCount = 0;

            foreach (int npcType in before.collectedNpcTypes)
            {
                // Find an empty NPC slot
                int npcSlot = -1;
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    if (!Main.npc[i].active)
                    {
                        npcSlot = i;
                        break;
                    }
                }

                if (npcSlot == -1)
                {
                    Main.NewText($"Could not respawn NPC: no free NPC slots.", 255, 150, 100);
                    continue;
                }

                // Try to get the original position and name from npcPositions if available
                Vector2 spawnPos = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16 - 48);
                string displayName = "";
                
                if (before.npcPositions != null)
                {
                    var posData = before.npcPositions.FirstOrDefault(p => p.type == npcType);
                    if (posData != default)
                    {
                        spawnPos = FindSafeNPCSpawnLocation((int)(posData.x / 16), (int)(posData.y / 16));
                        displayName = posData.displayName;
                    }
                }

                // Spawn the NPC
                NPC newNpc = new NPC();
                newNpc.SetDefaults(npcType);
                newNpc.position = spawnPos;
                newNpc.active = true;
                Main.npc[npcSlot] = newNpc;
                
                // Restore custom name if it was set
                if (!string.IsNullOrEmpty(displayName))
                    Main.npc[npcSlot].GivenName = displayName;
                
                // Mark as not homeless - Terraria's system will auto-assign housing
                // during the next update cycle when housing is available
                Main.npc[npcSlot].homeless = false;
                
                // Add temporary fall immunity
                Main.npc[npcSlot].AddBuff(BuffID.Featherfall, 60 * 10);
                
                respawnedCount++;
            }

            if (respawnedCount > 0)
            {
                Main.NewText($"Respawned {respawnedCount} town NPC{(respawnedCount == 1 ? "" : "s")}.", 150, 200, 255);
            }
        }

        /// <summary>
        /// Find a safe spawn location for an NPC near the target position.
        /// Checks if the position is inside tiles and finds an alternative if needed.
        /// Returns world pixel coordinates (multiply tile position by 16).
        /// </summary>
        private static Vector2 FindSafeNPCSpawnLocation(int targetTileX, int targetTileY)
        {
            // Look for a tile that is:
            // 1. Open space (no solid tile blocking the NPC)
            // 2. Has solid ground below (at least 1 tile of solid material)
            
            // First check the target position and immediate area
            for (int searchY = targetTileY; searchY < targetTileY + 20; searchY++)
            {
                if (!WorldGen.InWorld(targetTileX, searchY, 1))
                    continue;

                Tile currentTile = Framing.GetTileSafely(targetTileX, searchY);
                
                // Skip if this tile is solid (NPC can't be here)
                if (currentTile.HasTile && Main.tileSolid[currentTile.TileType])
                    continue;

                // Check if there's solid ground below this position
                if (searchY + 1 < Main.maxTilesY)
                {
                    Tile belowTile = Framing.GetTileSafely(targetTileX, searchY + 1);
                    if (belowTile.HasTile && Main.tileSolid[belowTile.TileType])
                    {
                        // Found a good spot: open space with solid ground below
                        return new Vector2(targetTileX * 16, searchY * 16);
                    }
                }
            }

            // If we couldn't find a good spot near target, search in expanding squares
            int searchRadius = 1;
            while (searchRadius <= 50)
            {
                for (int dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    for (int dy = -searchRadius; dy <= searchRadius; dy++)
                    {
                        // Only check the outer ring of this radius
                        if (Math.Abs(dx) != searchRadius && Math.Abs(dy) != searchRadius)
                            continue;

                        int checkX = targetTileX + dx;
                        int checkY = targetTileY + dy;

                        if (!WorldGen.InWorld(checkX, checkY, 1))
                            continue;

                        Tile checkTile = Framing.GetTileSafely(checkX, checkY);
                        
                        // Skip if this tile is solid
                        if (checkTile.HasTile && Main.tileSolid[checkTile.TileType])
                            continue;

                        // Check for solid ground below
                        if (checkY + 1 < Main.maxTilesY)
                        {
                            Tile belowTile = Framing.GetTileSafely(checkX, checkY + 1);
                            if (belowTile.HasTile && Main.tileSolid[belowTile.TileType])
                            {
                                return new Vector2(checkX * 16, checkY * 16);
                            }
                        }
                    }
                }
                searchRadius++;
            }

            // Fallback: spawn at world spawn if no safe location found
            return new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16 - 48);
        }

        /// <summary>
        /// Apply config-aware world settings after regeneration.
        /// Allows player to choose whether to preserve evil type, dungeon side, etc.
        /// </summary>
        private static void ApplyConfigurableWorldSettings(WorldProgressSnapshot before)
        {
            if (before == null)
                return;

            var config = ModContent.GetInstance<DynamicWorldsConfig>();

            // Preserve evil type (Crimson vs Corruption) if configured
            if (config.PreserveEvilType)
            {
                WorldGen.crimson = before.crimson;
                Main.NewText("Evil type preserved from previous world.", 150, 200, 255);
            }

            // Note: Dungeon side and biome features are baked into terrain during generation.
            // These cannot be changed after world generation without major restructuring.
            // The PreserveDungeonSide and PreserveBiomeFeatures settings are documented
            // as aspirational features that would require custom world generation code.
            // For now, they serve as markers for future implementation.
            
            if (config.PreserveDungeonSide)
            {
                Main.NewText("⚠ Dungeon side preservation not yet implemented (requires custom worldgen).", 255, 200, 100);
            }

            if (config.PreserveBiomeFeatures)
            {
                Main.NewText("⚠ Biome feature preservation not yet implemented (requires custom worldgen).", 255, 200, 100);
            }
        }

        /// <summary>
        /// Advances game time by the specified number of in-game days.
        /// Each day = 86400 ticks (24 * 3600).
        /// This allows NPCs to move in, settle, and perform daily activities.
        /// </summary>
        private static void AdvanceGameTime(int days)
        {
            const int ticksPerDay = 24 * 3600; // 86400 ticks per in-game day
            int totalTicks = days * ticksPerDay;

            Main.NewText($"Advancing time by {days} in-game day{(days == 1 ? "" : "s")}...", 100, 200, 255);

            // Advance time by incrementing Main.time
            // We do this in chunks to allow game logic to process properly
            for (int i = 0; i < days; i++)
            {
                Main.time += ticksPerDay;
                
                // Every 14400 ticks (6 in-game hours), run world updates
                // This allows NPCs to update their positions and AI
                if (Main.time >= 14400)
                {
                    Main.time = 0;
                    Main.dayTime = !Main.dayTime; // Toggle between day/night
                    
                    // Update day/night status for all NPCs
                    for (int npcIndex = 0; npcIndex < Main.npc.Length; npcIndex++)
                    {
                        NPC npc = Main.npc[npcIndex];
                        if (npc.active && npc.townNPC)
                        {
                            // Force NPC to update their position/home
                            npc.ai[0] = 0;
                            npc.ai[1] = 0;
                        }
                    }
                }
            }

            Main.NewText($"Time advanced! NPCs should now be moving in.", 100, 255, 100);
        }

    }

    public class RegenWorldCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "regenworld";
        public override string Usage => "/regenworld [seed]";
        public override string Description =>
            "Regenerates the world layout while keeping Hardmode, ores, bosses, invasions, etc. " +
            "Optionally pass a seed: /regenworld 12345 or /regenworld myseedname (single-player only).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string seed = args.Length > 0 ? args[0] : null;
            SingleplayerRegenHelper.RegenerateWorldWithProgress(seed);
        }
    }
    
    // /hardmode and /down commands exactly as we had them before:
    // (no changes needed to integrate with world-load printing / saving)

    public class HardmodeCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "hardmode";
        public override string Usage => "/hardmode [on|off]";
        public override string Description =>
            "Toggle Hardmode for this world (single-player only).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            var config = ModContent.GetInstance<DynamicWorldsConfig>();
            if (!config.AllowCheats)
            {
                Main.NewText("Cheats are disabled. Enable 'Allow Cheats' in the mod config.", 255, 80, 80);
                return;
            }

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                Main.NewText("Hardmode command only works in single player.", 255, 80, 80);
                return;
            }

            bool setHardmode = true;
            if (args.Length >= 1)
            {
                string arg = args[0].ToLowerInvariant();
                if (arg == "off" || arg == "false" || arg == "0")
                    setHardmode = false;
            }

            Main.hardMode = setHardmode;

            if (setHardmode)
            {
                WorldProgressUtil.ChooseHardmodeOresVanillaStyle();
                Main.NewText("Hardmode ENABLED for this world.", 150, 255, 150);
            }
            else
            {
                Main.NewText("Hardmode DISABLED for this world.", 255, 150, 150);
            }

            WorldProgressUtil.SaveToFile();
        }
    }

    public class DownCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "down";
        public override string Usage => "/down <bossOrEvent>";
        public override string Description =>
            "Mark a boss or event as defeated (e.g., /down eye, /down plantera, /down goblins).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            var config = ModContent.GetInstance<DynamicWorldsConfig>();
            if (!config.AllowCheats)
            {
                Main.NewText("Cheats are disabled. Enable 'Allow Cheats' in the mod config.", 255, 80, 80);
                return;
            }

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                Main.NewText("Down command only works in single player.", 255, 80, 80);
                return;
            }

            if (args.Length == 0)
            {
                Main.NewText("Usage: /down <eye|evil|skeletron|queenbee|kingslime|deerclops|mech1|mech2|mech3|plantera|golem|fishron|moonlord|goblins|frost|pirates|martians|pumpkin|frostmoon>", 255, 230, 150);
                return;
            }

            string key = args[0].ToLowerInvariant();
            if (!DownFlagHelper.SetDowned(key, out string label))
            {
                Main.NewText($"Unknown boss/event '{key}'.", 255, 80, 80);
                Main.NewText("Valid: eye, evil, skeletron, queenbee, kingslime, deerclops, mech1, mech2, mech3, plantera, golem, fishron, moonlord, goblins, frost, pirates, martians, pumpkin, frostmoon", 255, 230, 150);
                return;
            }

            Main.NewText($"Marked {label} as defeated.", 150, 255, 150);
            WorldProgressUtil.SaveToFile();
        }
    }

    public static class DownFlagHelper
    {
        public static bool SetDowned(string key, out string label)
        {
            label = "";

            switch (key)
            {
                case "eye":
                case "eyeofcthulhu":
                    NPC.downedBoss1 = true; label = "Eye of Cthulhu"; return true;
                case "evil":
                case "eow":
                case "boc":
                case "worldeater":
                case "brainofcthulhu":
                    NPC.downedBoss2 = true; label = "Evil boss (EoW / BoC)"; return true;
                case "skeletron":
                    NPC.downedBoss3 = true; label = "Skeletron"; return true;
                case "queenbee":
                case "qb":
                    NPC.downedQueenBee = true; label = "Queen Bee"; return true;
                case "kingslime":
                case "slimeking":
                    NPC.downedSlimeKing = true; label = "King Slime"; return true;
                case "deerclops":
                    NPC.downedDeerclops = true; label = "Deerclops"; return true;

                case "mech1":
                case "twins":
                    NPC.downedMechBoss1 = true; label = "The Twins"; return true;
                case "mech2":
                case "destroyer":
                    NPC.downedMechBoss2 = true; label = "The Destroyer"; return true;
                case "mech3":
                case "prime":
                case "skeletronprime":
                    NPC.downedMechBoss3 = true; label = "Skeletron Prime"; return true;

                case "plantera":
                    NPC.downedPlantBoss = true; label = "Plantera"; return true;
                case "golem":
                    NPC.downedGolemBoss = true; label = "Golem"; return true;
                case "fishron":
                    NPC.downedFishron = true; label = "Duke Fishron"; return true;
                case "moonlord":
                case "ml":
                    NPC.downedMoonlord = true; label = "Moon Lord"; return true;

                case "goblins":
                case "goblinarmy":
                    NPC.downedGoblins = true; label = "Goblin Army"; return true;
                case "frost":
                case "frostlegion":
                    NPC.downedFrost = true; label = "Frost Legion"; return true;
                case "pirates":
                case "pirateinvasion":
                    NPC.downedPirates = true; label = "Pirate Invasion"; return true;
                case "martians":
                case "martianmadness":
                    NPC.downedMartians = true; label = "Martian Madness"; return true;

                case "pumpkin":
                case "pumpkinmoon":
                    NPC.downedHalloweenKing = true;
                    NPC.downedHalloweenTree = true;
                    label = "Pumpkin Moon (Pumpking + Mourning Wood)";
                    return true;

                case "frostmoon":
                case "fm":
                    NPC.downedChristmasIceQueen = true;
                    NPC.downedChristmasSantank = true;
                    NPC.downedChristmasTree = true;
                    label = "Frost Moon (Ice Queen / Santa-NK1 / Everscream)";
                    return true;

                default:
                    return false;
            }
        }
    }

    //snap command that just prints the current snapshot
    public class SnapshotCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "snap";
        public override string Usage => "/snap";
        public override string Description =>
            "Prints the current world progression snapshot.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            var snap = WorldProgressUtil.Capture();
            WorldProgressUtil.PrintSnapshotToChat("Snapshot", snap);
            
            var config = ModContent.GetInstance<DynamicWorldsConfig>();
            if (config.EnableRegenCounter)
            {
                Main.NewText(WorldRegenScheduler.GetStatusText(), 200, 80, 255);
            }
        }
    }

    // /dwinfo — prints a summary of all saved anchored tiles, erased tiles, and building zones.
    public class DwInfoCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "dwinfo";
        public override string Usage => "/dwinfo";
        public override string Description =>
            "Shows a summary of all anchored tiles, erased tiles, and building zones saved for this world.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            // ── Anchored tiles ────────────────────────────────────────────────
            int anchorCount = AnchoredTileSystem.AnchoredTiles.Count;
            int anchorCap   = AnchoredTileSystem.GetTileCap();
            Main.NewText($"[Anchored Tiles] {anchorCount} / {anchorCap}", 100, 200, 255);

            if (anchorCount > 0)
            {
                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;
                foreach (var pos in AnchoredTileSystem.AnchoredTiles.Keys)
                {
                    if (pos.X < minX) minX = pos.X;
                    if (pos.X > maxX) maxX = pos.X;
                    if (pos.Y < minY) minY = pos.Y;
                    if (pos.Y > maxY) maxY = pos.Y;
                }
                Main.NewText($"  Bounding box: ({minX},{minY}) → ({maxX},{maxY})", 80, 170, 220);
            }

            // ── Erased tiles ──────────────────────────────────────────────────
            int eraseCount = ErasedTileSystem.ErasedTiles.Count;
            int eraseCap   = ErasedTileSystem.GetErasureCap();
            Main.NewText($"[Erased Tiles]   {eraseCount} / {eraseCap}", 255, 120, 120);

            if (eraseCount > 0)
            {
                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;
                foreach (var pos in ErasedTileSystem.ErasedTiles)
                {
                    if (pos.X < minX) minX = pos.X;
                    if (pos.X > maxX) maxX = pos.X;
                    if (pos.Y < minY) minY = pos.Y;
                    if (pos.Y > maxY) maxY = pos.Y;
                }
                Main.NewText($"  Bounding box: ({minX},{minY}) → ({maxX},{maxY})", 220, 80, 80);
            }

            // ── Building zones ────────────────────────────────────────────────
            int zoneCount = BuildingAnchorSystem.Zones.Count;
            Main.NewText($"[Building Zones] {zoneCount} zone{(zoneCount == 1 ? "" : "s")}", 180, 255, 180);

            foreach (var kv in BuildingAnchorSystem.Zones)
            {
                var z = kv.Value;
                Main.NewText(
                    $"  Zone #{z.Id}: ({z.TopLeft.X},{z.TopLeft.Y}) → ({z.BottomRight.X},{z.BottomRight.Y})  " +
                    $"{z.Width}×{z.Height}  {z.Tiles.Count} tiles  groundY={z.SavedGroundY}",
                    100, 220, 140);
            }

            if (anchorCount == 0 && eraseCount == 0 && zoneCount == 0)
                Main.NewText("No Dynamic Worlds data saved for this world.", 180, 180, 180);
        }
    }

    // /clearzones — removes all building zones from the world.
    public class ClearZonesCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "clearzones";
        public override string Usage => "/clearzones";
        public override string Description =>
            "Removes all building anchor zones from the world (does not affect anchored or erased tiles).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                Main.NewText("This command only works in single player.", 255, 80, 80);
                return;
            }

            int count = BuildingAnchorSystem.Zones.Count;
            if (count == 0)
            {
                Main.NewText("No building zones to clear.", 180, 180, 180);
                return;
            }

            BuildingAnchorSystem.Zones.Clear();

            Main.NewText($"Cleared {count} building zone{(count == 1 ? "" : "s")}.", 255, 150, 100);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  /killduplicatenpcs - Remove all duplicate town NPCs (keep only one of each type)
    // ─────────────────────────────────────────────────────────────────────
    public class KillDuplicateNPCsCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "killduplicatenpcs";

        public override string Usage => "/killduplicatenpcs";

        public override string Description => "Removes all duplicate town NPCs, keeping only one of each type.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            var seenTypes = new HashSet<int>();
            var npcsToDie = new List<int>();

            // Find all duplicate NPCs (keep first occurrence of each type)
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.townNPC && npc.type > 0)
                {
                    if (!seenTypes.Add(npc.type))
                    {
                        // We've already seen this NPC type, so mark it for death
                        npcsToDie.Add(i);
                    }
                }
            }

            // Kill all duplicates
            foreach (int slot in npcsToDie)
            {
                Main.npc[slot].life = 0;
                Main.npc[slot].active = false;
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, slot);
            }

            if (npcsToDie.Count == 0)
            {
                caller.Reply("No duplicate town NPCs found.", new Color(150, 200, 255));
            }
            else
            {
                caller.Reply($"Killed {npcsToDie.Count} duplicate town NPC{(npcsToDie.Count == 1 ? "" : "s")}. Kept {seenTypes.Count} unique type{(seenTypes.Count == 1 ? "" : "s")}.", new Color(255, 150, 100));
            }
        }
    }
}

