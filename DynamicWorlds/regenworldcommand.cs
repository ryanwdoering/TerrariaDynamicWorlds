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

            try
            {
                // Capture progression and chest snapshots before anything is wiped
                var before = WorldProgressUtil.Capture();
                WorldProgressUtil.PrintSnapshotToChat("Before regen", before);
                AnchoredTileSystem.RefreshAllChestSnapshots();

                // Snapshot personal spawn point before worldgen wipes state.
                Player p = Main.LocalPlayer;
                int savedSpawnX = p.SpawnX;
                int savedSpawnY = p.SpawnY;

                // Resolve seed: explicit arg → parse as int or hash the string, else random.
                int newSeed;
                if (!string.IsNullOrWhiteSpace(seedOverride))
                {
                    if (int.TryParse(seedOverride, out int parsedSeed))
                        newSeed = parsedSeed & 0x7FFFFFFF;
                    else
                        newSeed = Math.Abs(seedOverride.GetHashCode()) & 0x7FFFFFFF;

                    Main.NewText($"Using seed: {seedOverride} → {newSeed}", 180, 180, 255);
                }
                else
                {
                    newSeed = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
                }

                if (Main.ActiveWorldFileData != null)
                    Main.ActiveWorldFileData.SetSeed(newSeed.ToString());

                WorldGen.gen = true;
                WorldGen.clearWorld();

                var prog = new GenerationProgress();
                WorldGen.GenerateWorld(newSeed, prog);

                WorldGen.gen = false;

                // Re-apply all boss/hardmode/ore progression
                WorldProgressUtil.Apply(before);

                // First clear all tiles marked for erasure, then restore anchored tiles.
                // Order matters: erasure runs on the freshly generated world before anchors
                // are written back, so anchored tiles always win over erased positions.
                ErasedTileSystem.ClearAllErasedTiles();

                // Restore every anchored tile and chest
                AnchoredTileSystem.RestoreAllAnchoredTiles();

                // Teleport local player to the new spawn.
                // Priority: personal spawn point (bed) if it is anchored AND still valid → world spawn.
                // We assert SpawnX/SpawnY AFTER RestoreAllAnchoredTiles so the bed tile
                // is already back in the world before vanilla's CheckSpawn runs.
                Vector2 spawnPos;

                bool hasPersonalSpawn = savedSpawnX >= 0 && savedSpawnY >= 0;
                bool spawnIsAnchored  = hasPersonalSpawn &&
                    AnchoredTileSystem.AnchoredTiles.ContainsKey(new Terraria.DataStructures.Point16(savedSpawnX, savedSpawnY));

                // Validate the bed tile actually survived restoration (CheckSpawn checks
                // that the tile at SpawnY-1 is a valid spawn point and the area is clear).
                bool spawnIsValid = spawnIsAnchored && Player.CheckSpawn(savedSpawnX, savedSpawnY);

                if (spawnIsValid)
                {
                    // Re-assert personal spawn AFTER the bed is restored so vanilla accepts it.
                    p.SpawnX = savedSpawnX;
                    p.SpawnY = savedSpawnY;
                    // Spawn 3 tiles above the bed so the player isn't clipping into it.
                    spawnPos = new Vector2(savedSpawnX * 16, savedSpawnY * 16 - 48);
                    Main.NewText("Your anchored bed survived — spawning there.", 180, 255, 180);
                }
                else
                {
                    // Clear the stale bed spawn — the bed no longer exists in the new world.
                    p.SpawnX = -1;
                    p.SpawnY = -1;
                    // Spawn 3 tiles above world spawn to avoid clipping into terrain.
                    spawnPos = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16 - 48);

                    if (spawnIsAnchored && !spawnIsValid)
                        Main.NewText("Your bed was anchored but failed validation — spawning at world spawn.", 255, 150, 80);
                    else if (hasPersonalSpawn)
                        Main.NewText("Your bed was not anchored — spawning at world spawn.", 255, 200, 100);
                }

                p.Teleport(spawnPos, 1);
                p.fallStart = (int)(p.position.Y / 16f);

                // 10 seconds of featherfall so the player lands safely after teleport.
                // Buff durations are in game ticks (60 ticks = 1 second).
                p.AddBuff(Terraria.ID.BuffID.Featherfall, 60 * 10);

                var after = WorldProgressUtil.Capture();
                WorldProgressUtil.PrintSnapshotToChat("After regen", after);

                Main.NewText("World regeneration complete!", 80, 255, 80);
            }
            finally
            {
                WorldGen.gen  = false;
                regenRunning  = false;
            }
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
            Main.NewText(WorldRegenScheduler.GetStatusText(), 200, 80, 255);
        }
    }
}
