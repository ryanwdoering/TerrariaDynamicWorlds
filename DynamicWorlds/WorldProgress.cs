// WorldProgress.cs
using System;
using System.IO;
using System.Text.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent; // for GameModeData (world difficulty info)

namespace DynamicWorlds
{
    /// <summary>
    /// Snapshot of the world "progress" we care about between regenerations.
    /// This is what we persist to JSON next to the world file.
    /// </summary>
    public class WorldProgressSnapshot
    {
        // Core world state
        public bool hardMode;
        public bool crimson;   // true = Crimson, false = Corruption

        // World difficulty / game mode
        // 0 = Classic, 1 = Expert, 2 = Master, 3 = Journey
        public int gameMode;

        // Boss progression
        public bool downedBoss1;        // Eye of Cthulhu
        public bool downedBoss2;        // EoW/BoC
        public bool downedBoss3;        // Skeletron

        public bool downedQueenBee;
        public bool downedSlimeKing;
        public bool downedDeerclops;

        public bool downedWallOfFlesh;  // explicitly track WoF

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
                // core
                hardMode       = Main.hardMode,
                crimson        = WorldGen.crimson,
                gameMode       = Main.GameMode,

                // bosses
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

            return s;
        }

        /// <summary>
        /// Re-apply a previously captured snapshot to the current (freshly generated) world.
        /// </summary>
        public static void Apply(WorldProgressSnapshot s)
        {
            if (s == null)
                return;

            // Core stuff
            Main.hardMode    = s.hardMode;
            WorldGen.crimson = s.crimson;

            // World difficulty: match the previous world
            // GameMode: 0 = Classic, 1 = Expert, 2 = Master, 3 = Journey
            if (s.gameMode >= 0 && s.gameMode <= 3)
            {
                Main.GameMode      = s.gameMode;
                // Keep GameModeInfo in sync so enemy stats / drops line up
            }

            // Boss flags
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
            }
        }

        /// <summary>
        /// Load snapshot from disk (if present) and apply to current world.
        /// Called when the world loads (including an autocreated fresh world).
        /// </summary>
        public static void LoadFromFileAndApply()
        {
            try
            {
                string path = GetProgressFilePath();
                if (!File.Exists(path))
                    return;

                string json = File.ReadAllText(path);
                var snap = JsonSerializer.Deserialize<WorldProgressSnapshot>(json);
                Apply(snap);
            }
            catch
            {
                // If this fails, just skip applying; world will behave like a fresh one.
            }
        }
    }

    /// <summary>
    /// Hooks into tModLoader's world lifecycle to automatically
    /// save and restore world progression across world file regenerations.
    /// </summary>
    public class RoguelikeWorldSystem : ModSystem
    {
        public override void OnWorldLoad()
        {
            // World (including a brand-new regenerated one) has just loaded.
            WorldProgressUtil.LoadFromFileAndApply();
        }

        public override void OnWorldUnload()
        {
            // World is about to unload (SP exit, server stop, etc.) – snapshot progression.
            WorldProgressUtil.SaveToFile();
        }

        public override void PreSaveAndQuit()
        {
            // Called when the player hits "Save & Exit" from the menu.
            // This guarantees the JSON snapshot is up-to-date even if the game is closed normally,
            // not just on crashes or external server stop.
            WorldProgressUtil.SaveToFile();
        }
    }
}
