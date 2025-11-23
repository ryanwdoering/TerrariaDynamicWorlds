// RegenWorldCommand.cs
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;

namespace DynamicWorlds
{
    /// <summary>
    /// Helper that does the actual single-player world regeneration
    /// and prints debug info about world progression before/after.
    /// </summary>
    public static class SingleplayerRegenHelper
    {
        private static bool regenRunning = false;

        public static void RegenerateWorldWithProgress()
        {
            if (regenRunning)
            {
                Main.NewText("World regeneration is already in progress.", 255, 200, 50);
                return;
            }

            regenRunning = true;
            try
            {
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

                // Capture current progression (Hardmode, bosses, ore tiers, etc.)
                var before = WorldProgressUtil.Capture();
                PrintSnapshot("Before regen", before);

                Main.NewText("Regenerating world with preserved progression...", 200, 200, 255);

                // Pick a fresh random seed
                int newSeed = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);

                // Update the world file's seed if possible (not strictly required)
                if (Main.ActiveWorldFileData != null)
                {
                    Main.ActiveWorldFileData.SetSeed(newSeed.ToString());
                }

                // Run vanilla worldgen again in-place
                WorldGen.gen = true;
                Main.gameMenu = true; // worldgen expects this

                WorldGen.clearWorld();

                GenerationProgress progress = new GenerationProgress
                {
                    Message = "Generating a new world..."
                };

                WorldGen.GenerateWorld(newSeed, progress);

                WorldGen.gen = false;
                Main.gameMenu = false;

                // Re-apply progression flags and ore tiers to this fresh world
                WorldProgressUtil.Apply(before);

                // Teleport the local player to the new spawn
                Player p = Main.LocalPlayer;
                Vector2 spawnPos = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
                p.Teleport(spawnPos, 1);
                p.fallStart = (int)(p.position.Y / 16f);

                // Capture progression again after regen+apply, to verify behavior
                var after = WorldProgressUtil.Capture();
                PrintSnapshot("After regen", after);

                Main.NewText("World regeneration complete!", 80, 255, 80);
            }
            finally
            {
                regenRunning = false;
            }
        }

        private static void PrintSnapshot(string label, WorldProgressSnapshot s)
        {
            if (s == null)
            {
                Main.NewText($"{label}: snapshot is null", 255, 80, 80);
                return;
            }

            // Basic world state
            string evil = s.crimson ? "Crimson" : "Corruption";
            string mode = s.hardMode ? "Hardmode" : "Pre-Hardmode";

            Main.NewText($"{label} – {mode}, {evil}", 200, 220, 255);

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
        }

        private static string Flag(string name, bool downed)
            => downed ? $"[{name}✓]" : $"[{name} ]";

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
    /// Chat command: /regenworld
    /// Regenerates the world in single player while preserving progression,
    /// and prints debug info before / after to verify correctness.
    /// </summary>
    public class RegenWorldCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;

        public override string Command => "regenworld";

        public override string Usage => "/regenworld";

        public override string Description =>
            "Regenerates the world layout while keeping Hardmode, ores, and boss progression (single-player only).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            SingleplayerRegenHelper.RegenerateWorldWithProgress();
        }
    }
}
