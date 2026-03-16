using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace DynamicWorlds
{
    internal sealed class RegenExecutionResult
    {
        public bool HadSavedSpawn;
        public bool UsePersonalSpawn;
        public int SpawnTileX;
        public int SpawnTileY;
        public int RespawnedNpcCount;
        public int RestoredHousingCount;
    }

    internal readonly struct NpcRestoreSummary
    {
        public readonly int RespawnedCount;
        public readonly int RestoredHousingCount;

        public NpcRestoreSummary(int respawnedCount, int restoredHousingCount)
        {
            RespawnedCount = respawnedCount;
            RestoredHousingCount = restoredHousingCount;
        }
    }

    public static class SingleplayerRegenHelper
    {
        public static void RegenerateWorldWithProgress(string seedOverride = null)
        {
            if (DynamicWorldRegenSystem.IsBusy)
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

            PendingRegenContext pending = CreatePendingRegenContext(seedOverride);
            if (pending == null)
                return;

            Main.NewText("Saving current world and opening the regeneration screen...", 180, 220, 255);
            DynamicWorldRegenSystem.QueueRegen(pending);
        }

        internal static PendingRegenContext CreatePendingRegenContext(string seedOverride)
        {
            var mod = ModContent.GetInstance<DynamicWorlds>();
            mod.Logger.Info("=== WORLD REGENERATION STARTED ===");
            mod.Logger.Info("Capturing world state for menu-based regeneration...");

            WorldProgressSnapshot before = WorldProgressUtil.Capture();
            WorldProgressUtil.PrintSnapshotToChat("Before regen", before);

            AnchoredTileSystem.RefreshAllChestSnapshots();
            StructureAnchorSystem.RefreshAllChestSnapshots();

            Player player = Main.LocalPlayer;
            int newSeed = ResolveNewSeed(seedOverride, out string seedLabel);

            mod.Logger.Info($"Queued menu regen with seed {newSeed} and {AnchoredTileSystem.AnchoredTiles.Count} anchored tiles.");

            return new PendingRegenContext
            {
                Snapshot = CloneSnapshot(before),
                NewSeed = newSeed,
                SeedLabel = seedLabel,
                SavedSpawnX = player?.SpawnX ?? -1,
                SavedSpawnY = player?.SpawnY ?? -1,
                AnchoredTiles = new Dictionary<Point16, AnchoredTileData>(AnchoredTileSystem.AnchoredTiles),
                AnchoredChests = CloneAnchoredChests(),
                ErasedTiles = new HashSet<Point16>(ErasedTileSystem.ErasedTiles),
                BuildingZones = CloneBuildingZones(),
            };
        }

        internal static RegenExecutionResult ExecutePendingRegen(PendingRegenContext pending)
        {
            if (pending == null)
                return new RegenExecutionResult();

            var mod = ModContent.GetInstance<DynamicWorlds>();
            mod.Logger.Info("Applying preserved state to regenerated world...");

            CopyPendingDataToLiveSystems(pending);

            GenerationProgress progress = pending.Progress;
            if (progress != null)
            {
                progress.TotalWeight += 5.75d;
                WorldGenerator.CurrentGenerationProgress = progress;
            }

            RunProgressStep(progress, "Restoring world progression...", 1d, () =>
            {
                WorldProgressUtil.Apply(pending.Snapshot);
            });

            RunProgressStep(progress, "Applying world settings...", 0.5d, () =>
            {
                ApplyConfigurableWorldSettings(pending.Snapshot, announce: false);
            });

            RunProgressStep(progress, "Clearing erased tiles...", 0.75d, () =>
            {
                ErasedTileSystem.ClearAllErasedTiles(announce: false);
            });

            RunProgressStep(progress, "Restoring structure zones...", 1.25d, () =>
            {
                StructureAnchorSystem.RestoreAllZones(announce: false);
            });

            RunProgressStep(progress, "Restoring anchored tiles...", 1.25d, () =>
            {
                AnchoredTileSystem.RestoreAllAnchoredTiles(announce: false);
            });

            RegenExecutionResult result = DeterminePlayerPlacement(pending);

            RunProgressStep(progress, "Restoring town NPCs and housing...", 0.75d, () =>
            {
                NpcRestoreSummary summary = RespawnTownNPCsAtOriginalPositions(pending.Snapshot, announce: false);
                result.RespawnedNpcCount = summary.RespawnedCount;
                result.RestoredHousingCount = summary.RestoredHousingCount;
            });

            RunProgressStep(progress, "Finalizing regenerated world...", 0.25d, () =>
            {
                WorldProgressUtil.SaveToFile();
            });

            mod.Logger.Info("Preserved world state applied successfully.");
            return result;
        }

        private static WorldProgressSnapshot CloneSnapshot(WorldProgressSnapshot source)
        {
            if (source == null)
                return null;

            return new WorldProgressSnapshot
            {
                worldName = source.worldName,
                worldId = source.worldId,
                worldSeed = source.worldSeed,
                hardMode = source.hardMode,
                crimson = source.crimson,
                gameMode = source.gameMode,
                downedBoss1 = source.downedBoss1,
                downedBoss2 = source.downedBoss2,
                downedBoss3 = source.downedBoss3,
                downedQueenBee = source.downedQueenBee,
                downedSlimeKing = source.downedSlimeKing,
                downedDeerclops = source.downedDeerclops,
                downedMech1 = source.downedMech1,
                downedMech2 = source.downedMech2,
                downedMech3 = source.downedMech3,
                downedPlantera = source.downedPlantera,
                downedGolem = source.downedGolem,
                downedFishron = source.downedFishron,
                downedMoonLord = source.downedMoonLord,
                downedGoblins = source.downedGoblins,
                downedFrostLegion = source.downedFrostLegion,
                downedPirates = source.downedPirates,
                downedMartians = source.downedMartians,
                downedPumpkinMoonKing = source.downedPumpkinMoonKing,
                downedPumpkinMoonTree = source.downedPumpkinMoonTree,
                downedFrostMoonIceQueen = source.downedFrostMoonIceQueen,
                downedFrostMoonSantank = source.downedFrostMoonSantank,
                downedFrostMoonTree = source.downedFrostMoonTree,
                copperTier = source.copperTier,
                ironTier = source.ironTier,
                silverTier = source.silverTier,
                goldTier = source.goldTier,
                cobaltTier = source.cobaltTier,
                mythrilTier = source.mythrilTier,
                adamantiteTier = source.adamantiteTier,
                collectedNpcTypes = source.collectedNpcTypes != null ? new List<int>(source.collectedNpcTypes) : new List<int>(),
                npcPositions = source.npcPositions != null
                    ? new List<(int type, float x, float y, string displayName, string species)>(source.npcPositions)
                    : new List<(int type, float x, float y, string displayName, string species)>(),
                npcHousing = source.npcHousing != null
                    ? new List<(int type, int homeX, int homeY)>(source.npcHousing)
                    : new List<(int type, int homeX, int homeY)>(),
            };
        }

        private static Dictionary<Point16, SavedChestContents> CloneAnchoredChests()
        {
            var clone = new Dictionary<Point16, SavedChestContents>();
            foreach (var kv in AnchoredTileSystem.AnchoredChests)
                clone[kv.Key] = CloneChestContents(kv.Value);

            return clone;
        }

        private static SavedChestContents CloneChestContents(SavedChestContents source)
        {
            var clone = new SavedChestContents
            {
                Position = source.Position,
                Items = new Item[Chest.maxItems]
            };

            for (int i = 0; i < Chest.maxItems; i++)
                clone.Items[i] = source.Items != null && i < source.Items.Length && source.Items[i] != null
                    ? source.Items[i].Clone()
                    : new Item();

            return clone;
        }

        private static Dictionary<int, BuildingZone> CloneBuildingZones()
        {
            var clone = new Dictionary<int, BuildingZone>();
            foreach (var kv in StructureAnchorSystem.Zones)
                clone[kv.Key] = BuildingZone.FromTag(kv.Value.ToTag());

            return clone;
        }

        private static int ResolveNewSeed(string seedOverride, out string seedLabel)
        {
            var config = ModContent.GetInstance<DynamicWorldsConfig>();

            if (!string.IsNullOrWhiteSpace(seedOverride))
            {
                int newSeed = int.TryParse(seedOverride, out int parsedSeed)
                    ? parsedSeed & 0x7FFFFFFF
                    : Math.Abs(seedOverride.GetHashCode()) & 0x7FFFFFFF;

                seedLabel = seedOverride;
                Main.NewText($"Using seed: {seedOverride} -> {newSeed}", 180, 180, 255);
                return newSeed;
            }

            if (!config.RandomizeSeedEachRegen)
            {
                string currentSeedText = Main.ActiveWorldFileData?.SeedText ?? string.Empty;
                int newSeed = int.TryParse(currentSeedText, out int parsedSeed)
                    ? parsedSeed & 0x7FFFFFFF
                    : Math.Abs(currentSeedText.GetHashCode()) & 0x7FFFFFFF;

                seedLabel = string.IsNullOrWhiteSpace(currentSeedText) ? newSeed.ToString() : currentSeedText;
                Main.NewText($"Reusing world seed for consistent layout: {seedLabel}", 180, 180, 255);
                return newSeed;
            }

            int randomSeed = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
            seedLabel = randomSeed.ToString();
            Main.NewText($"Using random regen seed: {randomSeed}", 180, 180, 255);
            return randomSeed;
        }

        private static void CopyPendingDataToLiveSystems(PendingRegenContext pending)
        {
            AnchoredTileSystem.AnchoredTiles.Clear();
            foreach (var kv in pending.AnchoredTiles)
                AnchoredTileSystem.AnchoredTiles[kv.Key] = kv.Value;

            AnchoredTileSystem.AnchoredChests.Clear();
            foreach (var kv in pending.AnchoredChests)
                AnchoredTileSystem.AnchoredChests[kv.Key] = CloneChestContents(kv.Value);

            ErasedTileSystem.ErasedTiles.Clear();
            foreach (Point16 pos in pending.ErasedTiles)
                ErasedTileSystem.ErasedTiles.Add(pos);

            StructureAnchorSystem.Zones.Clear();
            foreach (var kv in pending.BuildingZones)
                StructureAnchorSystem.Zones[kv.Key] = kv.Value;
            StructureAnchorSystem.RecalculateNextId();
        }

        private static RegenExecutionResult DeterminePlayerPlacement(PendingRegenContext pending)
        {
            var result = new RegenExecutionResult
            {
                HadSavedSpawn = pending.SavedSpawnX >= 0 && pending.SavedSpawnY >= 0,
                SpawnTileX = Main.spawnTileX,
                SpawnTileY = Main.spawnTileY
            };

            if (!result.HadSavedSpawn)
                return result;

            Point16 savedSpawn = new Point16(pending.SavedSpawnX, pending.SavedSpawnY);
            Point16 effectiveSpawn = savedSpawn;

            if (StructureAnchorSystem.TryTranslateSavedPoint(savedSpawn, out Point16 translatedSpawn))
                effectiveSpawn = translatedSpawn;

            if (effectiveSpawn.X >= 0 && effectiveSpawn.Y >= 0 && Player.CheckSpawn(effectiveSpawn.X, effectiveSpawn.Y))
            {
                result.UsePersonalSpawn = true;
                result.SpawnTileX = effectiveSpawn.X;
                result.SpawnTileY = effectiveSpawn.Y;
            }

            return result;
        }

        /// <summary>
        /// Respawn town NPCs and restore preserved housing assignments.
        /// </summary>
        private static NpcRestoreSummary RespawnTownNPCsAtOriginalPositions(WorldProgressSnapshot before, bool announce)
        {
            if (before?.collectedNpcTypes == null || before.collectedNpcTypes.Count == 0)
                return new NpcRestoreSummary(0, 0);

            int respawnedCount = 0;
            int restoredHousingCount = 0;

            foreach (int npcType in before.collectedNpcTypes)
            {
                int npcSlot = FindExistingNpcSlot(npcType);
                bool createdNpc = false;

                if (npcSlot == -1)
                {
                    npcSlot = FindInactiveNpcSlot();
                    createdNpc = npcSlot != -1;
                }

                if (npcSlot == -1)
                {
                    if (announce)
                        Main.NewText("Could not respawn NPC: no free NPC slots.", 255, 150, 100);

                    continue;
                }

                Vector2 spawnPos = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16 - 48);
                string displayName = "";
                bool restoredHousing = TryResolvePreservedHousing(before, npcType, out Point16 resolvedHome);
                
                if (before.npcPositions != null)
                {
                    var posData = before.npcPositions.FirstOrDefault(p => p.type == npcType);
                    if (posData != default)
                    {
                        spawnPos = FindSafeNPCSpawnLocation((int)(posData.x / 16), (int)(posData.y / 16));
                        displayName = posData.displayName;
                    }
                }

                if (restoredHousing)
                    spawnPos = FindSafeNPCSpawnLocation(resolvedHome.X, Math.Max(10, resolvedHome.Y - 3));

                if (createdNpc)
                    Main.npc[npcSlot] = new NPC();

                Main.npc[npcSlot].SetDefaults(npcType);
                Main.npc[npcSlot].whoAmI = npcSlot;
                Main.npc[npcSlot].position = spawnPos;
                Main.npc[npcSlot].active = true;

                if (!string.IsNullOrEmpty(displayName))
                    Main.npc[npcSlot].GivenName = displayName;

                if (restoredHousing)
                {
                    Main.npc[npcSlot].homeTileX = resolvedHome.X;
                    Main.npc[npcSlot].homeTileY = resolvedHome.Y;
                    Main.npc[npcSlot].homeless = false;
                    WorldGen.TownManager.KickOut(Main.npc[npcSlot].type);
                    WorldGen.TownManager.SetRoom(Main.npc[npcSlot].type, resolvedHome.X, resolvedHome.Y);
                    restoredHousingCount++;
                }
                else
                {
                    Main.npc[npcSlot].homeTileX = -1;
                    Main.npc[npcSlot].homeTileY = -1;
                    Main.npc[npcSlot].homeless = true;
                    WorldGen.TownManager.KickOut(Main.npc[npcSlot].type);
                }

                Main.npc[npcSlot].netUpdate = true;
                respawnedCount++;
            }

            if (announce && respawnedCount > 0)
            {
                Main.NewText($"Respawned {respawnedCount} town NPC{(respawnedCount == 1 ? "" : "s")}.", 150, 200, 255);
                if (restoredHousingCount > 0)
                    Main.NewText($"Reassigned {restoredHousingCount} preserved home{(restoredHousingCount == 1 ? "" : "s")}.", 180, 255, 180);
            }

            return new NpcRestoreSummary(respawnedCount, restoredHousingCount);
        }

        private static int FindExistingNpcSlot(int npcType)
        {
            for (int i = 0; i < Main.npc.Length; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == npcType && Main.npc[i].townNPC)
                    return i;
            }

            return -1;
        }

        private static int FindInactiveNpcSlot()
        {
            for (int i = 0; i < Main.npc.Length; i++)
            {
                if (!Main.npc[i].active)
                    return i;
            }

            return -1;
        }

        private static bool TryResolvePreservedHousing(WorldProgressSnapshot before, int npcType, out Point16 resolvedHome)
        {
            resolvedHome = default;

            if (before?.npcHousing == null || before.npcHousing.Count == 0)
                return false;

            var housingData = before.npcHousing.FirstOrDefault(h => h.type == npcType);
            if (housingData == default || housingData.homeX < 0 || housingData.homeY < 0)
                return false;

            var candidateHomes = new HashSet<Point16>();
            Point16 savedHome = new Point16(housingData.homeX, housingData.homeY);

            if (StructureAnchorSystem.TryTranslateSavedPoint(savedHome, out Point16 translatedZoneHome))
                candidateHomes.Add(translatedZoneHome);

            if (IsAnchoredHousingCandidate(savedHome))
                candidateHomes.Add(savedHome);

            foreach (Point16 candidate in candidateHomes)
            {
                if (TryValidateHousingCandidate(npcType, candidate, out resolvedHome))
                    return true;
            }

            return false;
        }

        private static bool IsAnchoredHousingCandidate(Point16 savedHome)
        {
            for (int dx = -8; dx <= 8; dx++)
            {
                for (int dy = -6; dy <= 2; dy++)
                {
                    var check = new Point16(savedHome.X + dx, savedHome.Y + dy);
                    if (AnchoredTileSystem.AnchoredTiles.ContainsKey(check))
                        return true;
                }
            }

            return false;
        }

        private static bool TryValidateHousingCandidate(int npcType, Point16 candidateHome, out Point16 resolvedHome)
        {
            resolvedHome = default;

            int roomCheckX = candidateHome.X;
            int roomCheckY = candidateHome.Y - 1;

            if (!WorldGen.InWorld(roomCheckX, roomCheckY, 10))
                return false;

            if (!WorldGen.StartRoomCheck(roomCheckX, roomCheckY))
                return false;

            if (!WorldGen.RoomNeeds(npcType))
                return false;

            WorldGen.ScoreRoom(-1, npcType);
            if (WorldGen.hiScore <= 0)
                return false;

            resolvedHome = new Point16((short)WorldGen.bestX, (short)WorldGen.bestY);
            return true;
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
        private static void ApplyConfigurableWorldSettings(WorldProgressSnapshot before, bool announce)
        {
            if (before == null)
                return;

            var config = ModContent.GetInstance<DynamicWorldsConfig>();

            // Preserve evil type (Crimson vs Corruption) if configured
            if (config.PreserveEvilType)
            {
                WorldGen.crimson = before.crimson;
                if (announce)
                    Main.NewText("Evil type preserved from previous world.", 150, 200, 255);
            }

            // Note: Dungeon side and biome features are baked into terrain during generation.
            // These cannot be changed after world generation without major restructuring.
            // The PreserveDungeonSide and PreserveBiomeFeatures settings are documented
            // as aspirational features that would require custom world generation code.
            // For now, they serve as markers for future implementation.
            
            if (announce && config.PreserveDungeonSide)
            {
                Main.NewText("Dungeon side preservation is not implemented yet.", 255, 200, 100);
            }

            if (announce && config.PreserveBiomeFeatures)
            {
                Main.NewText("Biome feature preservation is not implemented yet.", 255, 200, 100);
            }
        }

        private static void RunProgressStep(GenerationProgress progress, string message, double weight, Action action)
        {
            if (progress == null)
            {
                action();
                return;
            }

            progress.Message = message;
            progress.Start(weight);
            progress.Set(0.05d);
            action();
            progress.Set(1d);
            progress.End();
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
            "Prints the current world progression snapshot and scheduled regen status.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            var snap = WorldProgressUtil.Capture();
            WorldProgressUtil.PrintSnapshotToChat("Snapshot", snap);

            Main.NewText(WorldRegenScheduler.GetStatusText(), 200, 80, 255);
        }
    }

    // /dwinfo — prints a summary of all saved anchored tiles, erased tiles, and structure zones.
    public class DwInfoCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "dwinfo";
        public override string Usage => "/dwinfo";
        public override string Description =>
            "Shows a summary of all anchored tiles, erased tiles, and structure zones saved for this world.";

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
            int zoneCount = StructureAnchorSystem.Zones.Count;
            Main.NewText($"[Structure Zones] {zoneCount} zone{(zoneCount == 1 ? "" : "s")}", 180, 255, 180);

            foreach (var kv in StructureAnchorSystem.Zones)
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

    // /clearzones — removes all structure zones from the world.
    public class ClearZonesCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "clearzones";
        public override string Usage => "/clearzones";
        public override string Description =>
            "Removes all structure anchor zones from the world (does not affect anchored or erased tiles).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                Main.NewText("This command only works in single player.", 255, 80, 80);
                return;
            }

            int count = StructureAnchorSystem.Zones.Count;
            if (count == 0)
            {
                Main.NewText("No structure zones to clear.", 180, 180, 180);
                return;
            }

            StructureAnchorSystem.Zones.Clear();
            Main.NewText($"Cleared {count} structure zone{(count == 1 ? "" : "s")}.", 255, 150, 100);
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
