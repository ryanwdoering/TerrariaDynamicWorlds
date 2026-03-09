using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DynamicWorlds
{
    /// <summary>
    /// Snapshot of the world "progress" we care about between regenerations.
    /// This is what we persist to JSON next to the world file.
    /// NOTE: Wall of Flesh is NOT tracked; it is implied by hardMode.
    /// </summary>
    public class WorldProgressSnapshot
    {
        // --- World identity (ties snapshot permanently to a specific world) ---
        public string worldName;
        public int worldId;         // Main.worldID
        public string worldSeed;    // Main.ActiveWorldFileData.SeedText (if available)

        // Core world state
        public bool hardMode;
        public bool crimson;   // true = Crimson, false = Corruption

        // World difficulty / game mode
        // 0 = Classic, 1 = Expert, 2 = Master, 3 = Journey
        public int gameMode;

        // Boss progression (Wall of Flesh is derived from hardMode)
        public bool downedBoss1;        // Eye of Cthulhu
        public bool downedBoss2;        // EoW/BoC
        public bool downedBoss3;        // Skeletron

        public bool downedQueenBee;
        public bool downedSlimeKing;
        public bool downedDeerclops;

        public bool downedMech1;        // Twins
        public bool downedMech2;        // Destroyer
        public bool downedMech3;        // Skeletron Prime
        public bool downedPlantera;
        public bool downedGolem;
        public bool downedFishron;
        public bool downedMoonLord;

        // Invasions / seasonal events
        public bool downedGoblins;              // Goblin Army
        public bool downedFrostLegion;          // Frost Legion
        public bool downedPirates;              // Pirate Invasion
        public bool downedMartians;             // Martian Madness

        public bool downedPumpkinMoonKing;      // Pumpking
        public bool downedPumpkinMoonTree;      // Mourning Wood

        public bool downedFrostMoonIceQueen;    // Ice Queen
        public bool downedFrostMoonSantank;     // Santa-NK1
        public bool downedFrostMoonTree;        // Everscream

        // Ore tiers – these are the ones stored in WorldGen.SavedOreTiers.
        // Pre-hardmode:
        public int copperTier;
        public int ironTier;
        public int silverTier;
        public int goldTier;

        // Hardmode:
        public int cobaltTier;
        public int mythrilTier;
        public int adamantiteTier;

        // Town NPCs present in the world before regen (by NPC type ID).
        // These will be respawned at spawn after world regeneration.
        public List<int> collectedNpcTypes = new();
    }

    public static class WorldProgressUtil
    {
        /// <summary>
        /// Compute path: WorldFolder/WorldName_progress.json
        /// If anything goes wrong, fall back to a Mod save folder.
        /// </summary>
        private static string GetProgressFilePath()
        {
            try
            {
                if (Main.ActiveWorldFileData != null)
                {
                    string worldPath = Main.ActiveWorldFileData.Path;
                    string dir = Path.GetDirectoryName(worldPath)!;
                    string name = Path.GetFileNameWithoutExtension(worldPath);
                    return Path.Combine(dir, name + "_progress.json");
                }
            }
            catch
            {
                // Fall through to fallback path.
            }

            string fallbackDir = Path.Combine(Main.SavePath, "DynamicWorlds");
            Directory.CreateDirectory(fallbackDir);
            return Path.Combine(fallbackDir, "WorldProgress.json");
        }

        /// <summary>
        /// Capture current world progression into a snapshot object.
        /// </summary>
        public static WorldProgressSnapshot Capture()
        {
            var s = new WorldProgressSnapshot
            {
                // --- identity ---
                worldName = Main.worldName,
                worldId   = Main.worldID,
                worldSeed = Main.ActiveWorldFileData?.SeedText ?? string.Empty,

                // core
                hardMode       = Main.hardMode,
                crimson        = WorldGen.crimson,
                gameMode       = Main.GameMode,

                // bosses (Wall of Flesh is implied by hardMode)
                downedBoss1       = NPC.downedBoss1,
                downedBoss2       = NPC.downedBoss2,
                downedBoss3       = NPC.downedBoss3,
                downedQueenBee    = NPC.downedQueenBee,
                downedSlimeKing   = NPC.downedSlimeKing,
                downedDeerclops   = NPC.downedDeerclops,

                downedMech1       = NPC.downedMechBoss1,
                downedMech2       = NPC.downedMechBoss2,
                downedMech3       = NPC.downedMechBoss3,
                downedPlantera    = NPC.downedPlantBoss,
                downedGolem       = NPC.downedGolemBoss,
                downedFishron     = NPC.downedFishron,
                downedMoonLord    = NPC.downedMoonlord,

                // invasions / seasonal events
                downedGoblins            = NPC.downedGoblins,
                downedFrostLegion        = NPC.downedFrost,
                downedPirates            = NPC.downedPirates,
                downedMartians           = NPC.downedMartians,

                downedPumpkinMoonKing    = NPC.downedHalloweenKing,
                downedPumpkinMoonTree    = NPC.downedHalloweenTree,

                downedFrostMoonIceQueen  = NPC.downedChristmasIceQueen,
                downedFrostMoonSantank   = NPC.downedChristmasSantank,
                downedFrostMoonTree      = NPC.downedChristmasTree,

                // Pre-hardmode ore tiers (Copper/Tin, Iron/Lead, Silver/Tungsten, Gold/Platinum)
                copperTier     = WorldGen.SavedOreTiers.Copper,
                ironTier       = WorldGen.SavedOreTiers.Iron,
                silverTier     = WorldGen.SavedOreTiers.Silver,
                goldTier       = WorldGen.SavedOreTiers.Gold,

                // Hardmode ore tiers (Cobalt/Palladium, Mythril/Orichalcum, Adamantite/Titanium)
                cobaltTier     = WorldGen.SavedOreTiers.Cobalt,
                mythrilTier    = WorldGen.SavedOreTiers.Mythril,
                adamantiteTier = WorldGen.SavedOreTiers.Adamantite
            };

            // Collect all active town NPCs (alive, flagged as townNPC, valid type).
            s.collectedNpcTypes = Main.npc
                .Where(n => n.active && n.townNPC && n.type > 0)
                .Select(n => n.type)
                .Distinct()
                .ToList();

            return s;
        }

        /// <summary>
        /// Re-apply a previously captured snapshot to the current (freshly generated) world.
        /// </summary>
        public static void Apply(WorldProgressSnapshot s)
        {
            if (s == null)
                return;

            // Core world state
            Main.hardMode    = s.hardMode;
            WorldGen.crimson = s.crimson;

            // World difficulty: match the previous world
            // GameMode: 0 = Classic, 1 = Expert, 2 = Master, 3 = Journey
            if (s.gameMode >= 0 && s.gameMode <= 3)
            {
                Main.GameMode = s.gameMode;
            }

            // Boss flags (except WoF, handled above)
            NPC.downedBoss1        = s.downedBoss1;
            NPC.downedBoss2        = s.downedBoss2;
            NPC.downedBoss3        = s.downedBoss3;
            NPC.downedQueenBee     = s.downedQueenBee;
            NPC.downedSlimeKing    = s.downedSlimeKing;
            NPC.downedDeerclops    = s.downedDeerclops;

            NPC.downedMechBoss1    = s.downedMech1;
            NPC.downedMechBoss2    = s.downedMech2;
            NPC.downedMechBoss3    = s.downedMech3;
            NPC.downedPlantBoss    = s.downedPlantera;
            NPC.downedGolemBoss    = s.downedGolem;
            NPC.downedFishron      = s.downedFishron;
            NPC.downedMoonlord     = s.downedMoonLord;

            // Invasion / seasonal flags
            NPC.downedGoblins          = s.downedGoblins;
            NPC.downedFrost            = s.downedFrostLegion;
            NPC.downedPirates          = s.downedPirates;
            NPC.downedMartians         = s.downedMartians;

            NPC.downedHalloweenKing    = s.downedPumpkinMoonKing;
            NPC.downedHalloweenTree    = s.downedPumpkinMoonTree;

            NPC.downedChristmasIceQueen = s.downedFrostMoonIceQueen;
            NPC.downedChristmasSantank  = s.downedFrostMoonSantank;
            NPC.downedChristmasTree     = s.downedFrostMoonTree;

            // Pre-hardmode ore tiers – only overwrite if they look valid (> 0).
            if (s.copperTier     > 0) WorldGen.SavedOreTiers.Copper      = s.copperTier;
            if (s.ironTier       > 0) WorldGen.SavedOreTiers.Iron        = s.ironTier;
            if (s.silverTier     > 0) WorldGen.SavedOreTiers.Silver      = s.silverTier;
            if (s.goldTier       > 0) WorldGen.SavedOreTiers.Gold        = s.goldTier;

            // Hardmode ore tiers – this is what you really care about for "matching progress".
            if (s.cobaltTier     > 0) WorldGen.SavedOreTiers.Cobalt      = s.cobaltTier;
            if (s.mythrilTier    > 0) WorldGen.SavedOreTiers.Mythril     = s.mythrilTier;
            if (s.adamantiteTier > 0) WorldGen.SavedOreTiers.Adamantite  = s.adamantiteTier;

            // If the snapshot says we *should* be in Hardmode but the ore tiers were never set,
            // fall back to vanilla-style Hardmode ore selection (SmashAltar logic).
            if (s.hardMode &&
                (WorldGen.SavedOreTiers.Cobalt <= 0 ||
                 WorldGen.SavedOreTiers.Mythril <= 0 ||
                 WorldGen.SavedOreTiers.Adamantite <= 0))
            {
                ChooseHardmodeOresVanillaStyle();
            }

            // Respawn all town NPCs that were present before the regen.
            // SpawnOnPlayer places the NPC near the local player's spawn.
            if (s.collectedNpcTypes != null)
            {
                foreach (int npcType in s.collectedNpcTypes)
                {
                    // Skip if this NPC type is already alive in the world.
                    bool alreadyPresent = Main.npc.Any(n => n.active && n.type == npcType);
                    if (!alreadyPresent)
                        NPC.SpawnOnPlayer(Main.myPlayer, npcType);
                }
            }
        }

        /// <summary>
        /// Vanilla-style Hardmode ore selection, based on what SmashAltar does:
        ///   Cobalt or Palladium, Mythril or Orichalcum, Adamantite or Titanium.
        /// Use this when turning a world into Hardmode for the first time and you
        /// don't have previous ore tiers to copy.
        /// </summary>
        public static void ChooseHardmodeOresVanillaStyle()
        {
            if (WorldGen.SavedOreTiers.Cobalt <= 0)
            {
                WorldGen.SavedOreTiers.Cobalt =
                    Main.rand.NextBool()
                        ? TileID.Cobalt
                        : TileID.Palladium;
            }

            if (WorldGen.SavedOreTiers.Mythril <= 0)
            {
                WorldGen.SavedOreTiers.Mythril =
                    Main.rand.NextBool()
                        ? TileID.Mythril
                        : TileID.Orichalcum;
            }

            if (WorldGen.SavedOreTiers.Adamantite <= 0)
            {
                WorldGen.SavedOreTiers.Adamantite =
                    Main.rand.NextBool()
                        ? TileID.Adamantite
                        : TileID.Titanium;
            }
        }

        /// <summary>
        /// Save current snapshot to disk as JSON.
        /// </summary>
        public static void SaveToFile()
        {
            try
            {
                var snap = Capture();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(snap, options);
                string path = GetProgressFilePath();

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, json);
            }
            catch
            {
                // If this fails, progression just doesn't persist this time.
                Main.NewText("Warning: Failed to save world progression snapshot.", 255, 80, 80);
            }
        }

     
        /// <summary>
        /// Print a snapshot summary to chat (used on world load and regen).
        /// </summary>
        public static void PrintSnapshotToChat(string label, WorldProgressSnapshot s)
        {
            if (s == null)
            {
                Main.NewText($"{label}: snapshot is null", 255, 80, 80);
                return;
            }

            string evil = s.crimson ? "Crimson" : "Corruption";
            string mode = s.hardMode ? "Hardmode" : "Pre-Hardmode";

            Main.NewText($"{label} – {mode}, {evil} (GameMode={s.gameMode}, WorldID={s.worldId})", 200, 220, 255);

            // Pre-hardmode ore tiers
            string preOres =
                $"{OreName(s.copperTier)}, " +
                $"{OreName(s.ironTier)}, " +
                $"{OreName(s.silverTier)}, " +
                $"{OreName(s.goldTier)}";

            Main.NewText($"  Pre-HM ores  → {preOres}", 180, 220, 180);

            // Hardmode ore tiers
            string hmOres =
                $"{OreName(s.cobaltTier)}, " +
                $"{OreName(s.mythrilTier)}, " +
                $"{OreName(s.adamantiteTier)}";

            Main.NewText($"  HM ores      → {hmOres}", 180, 220, 180);

            // Boss summary (short flags)
            string bosses =
                $"{Flag("Eye", s.downedBoss1)} " +
                $"{Flag("Evil", s.downedBoss2)} " +
                $"{Flag("Skeletron", s.downedBoss3)} " +
                $"{Flag("QueenBee", s.downedQueenBee)} " +
                $"{Flag("KingSlime", s.downedSlimeKing)} " +
                $"{Flag("Deerclops", s.downedDeerclops)} " +
                $"{Flag("Mechs", s.downedMech1 || s.downedMech2 || s.downedMech3)} " +
                $"{Flag("Plantera", s.downedPlantera)} " +
                $"{Flag("Golem", s.downedGolem)} " +
                $"{Flag("Fishron", s.downedFishron)} " +
                $"{Flag("MoonLord", s.downedMoonLord)}";

            Main.NewText($"  Bosses       → {bosses}", 220, 200, 160);

            string invasions =
                $"{Flag("Goblins", s.downedGoblins)} " +
                $"{Flag("FrostLegion", s.downedFrostLegion)} " +
                $"{Flag("Pirates", s.downedPirates)} " +
                $"{Flag("Martians", s.downedMartians)} " +
                $"{Flag("Pumpkin", s.downedPumpkinMoonKing || s.downedPumpkinMoonTree)} " +
                $"{Flag("FrostMoon", s.downedFrostMoonIceQueen || s.downedFrostMoonSantank || s.downedFrostMoonTree)}";

            Main.NewText($"  Invasions    → {invasions}", 200, 220, 200);

            // Town NPCs
            if (s.collectedNpcTypes != null && s.collectedNpcTypes.Count > 0)
            {
                string npcNames = string.Join(", ", s.collectedNpcTypes.Select(NpcName));
                Main.NewText($"  Town NPCs    → {npcNames}", 200, 200, 255);
            }
            else
            {
                Main.NewText($"  Town NPCs    → (none)", 200, 200, 255);
            }
        }

        private static string Flag(string name, bool downed)
            => downed ? $"[{name}✓]" : $"[{name} ]";

        private static string NpcName(int npcType)
        {
            switch (npcType)
            {
                case NPCID.Guide:             return "Guide";
                case NPCID.Merchant:          return "Merchant";
                case NPCID.Nurse:             return "Nurse";
                case NPCID.Demolitionist:     return "Demolitionist";
                case NPCID.DyeTrader:         return "Dye Trader";
                case NPCID.Dryad:             return "Dryad";
                case NPCID.Painter:           return "Painter";
                case NPCID.GoblinTinkerer:    return "Goblin Tinkerer";
                case NPCID.Clothier:          return "Clothier";
                case NPCID.ArmsDealer:        return "Arms Dealer";
                case NPCID.Mechanic:          return "Mechanic";
                case NPCID.SantaClaus:        return "Santa Claus";
                case NPCID.Truffle:           return "Truffle";
                case NPCID.Wizard:            return "Wizard";
                case NPCID.Stylist:           return "Stylist";
                case NPCID.Pirate:            return "Pirate";
                case NPCID.Steampunker:       return "Steampunker";
                case NPCID.Cyborg:            return "Cyborg";
                case NPCID.TaxCollector:      return "Tax Collector";
                case NPCID.DD2Bartender:      return "Tavernkeep";
                case NPCID.Golfer:            return "Golfer";
                case NPCID.BestiaryGirl:      return "Zoologist";
                case NPCID.Princess:          return "Princess";
                case NPCID.TownSlimeBlue:
                case NPCID.TownSlimePurple:
                case NPCID.TownSlimeRed:
                case NPCID.TownSlimeYellow:
                case NPCID.TownSlimeGreen:
                case NPCID.TownSlimeOld:
                case NPCID.TownSlimeCopper:
                case NPCID.TownSlimeRainbow:  return "Town Slime";
                case NPCID.OldMan:            return "Old Man";
                default:                      return $"NPC({npcType})";
            }
        }

        private static string OreName(int tileId)
        {
            switch (tileId)
            {
                case TileID.Copper:      return "Copper";
                case TileID.Tin:         return "Tin";
                case TileID.Iron:        return "Iron";
                case TileID.Lead:        return "Lead";
                case TileID.Silver:      return "Silver";
                case TileID.Tungsten:    return "Tungsten";
                case TileID.Gold:        return "Gold";
                case TileID.Platinum:    return "Platinum";

                case TileID.Cobalt:      return "Cobalt";
                case TileID.Palladium:   return "Palladium";
                case TileID.Mythril:     return "Mythril";
                case TileID.Orichalcum:  return "Orichalcum";
                case TileID.Adamantite:  return "Adamantite";
                case TileID.Titanium:    return "Titanium";

                default:
                    if (tileId <= 0)
                        return "Unset";
                    return $"TileID {tileId}";
            }
        }
    }

    /// <summary>
    /// Hooks into tModLoader's world lifecycle to automatically
    /// save and restore world progression across world file regenerations.
    /// Also prints world info when entering the game.
    /// </summary>
    public class RoguelikeWorldSystem : ModSystem
    {
        /// <summary>
        /// Called after LoadWorldData; at this point the world is ready
        /// to be entered. We just print debug info here.
        /// </summary>
        public override void PostWorldLoad()
        {
        
            var snap = WorldProgressUtil.Capture();
            WorldProgressUtil.PrintSnapshotToChat("World loaded", snap);
        }

        /// <summary>
        /// World is about to unload (single-player exit, server stop, etc.).
        /// Capture and persist the latest progression for this world.
        /// </summary>
        public override void OnWorldUnload()
        {
            WorldProgressUtil.SaveToFile();
        }

        /// <summary>
        /// Called when the player hits "Save & Exit" from the menu
        /// on the local client. Ensures snapshot is up-to-date.
        /// </summary>
        public override void PreSaveAndQuit()
        {
            WorldProgressUtil.SaveToFile();
        }
    }
}
