using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DynamicWorlds
{
    // -------------------------------------------------------------------------
    //  A single saved structure zone: bounds + full per-tile snapshot.
    //  Tiles are stored relative to the zone's top-left so translation is just
    //  adding deltaY to every position when restoring.
    // -------------------------------------------------------------------------
    public class BuildingZone
    {
        // World-coordinate bounds at the time of capture
        public Point16 TopLeft;
        public Point16 BottomRight;

        // Ground-reference Y: the surface tile Y at the center column when captured.
        // After regen we compare this to the new surface to compute deltaY.
        public int SavedGroundY;

        // Full tile snapshots, keyed by world position
        public Dictionary<Point16, AnchoredTileData> Tiles = new();

        // Unique id so multiple Structure Anchors can coexist
        public int Id;

        // Chest contents for any chests captured inside the zone.
        // Keyed by original world top-left position of the chest.
        public Dictionary<Point16, SavedChestContents> Chests = new();

        // If the player's spawn (bed) is inside this zone, we save it here
        // so we can translate it by deltaY after restore.
        // (-1,-1) means no spawn was inside this zone at capture time.
        public Point16 SavedSpawn = new Point16(-1, -1);

        // Width / height helpers
        public int Width  => BottomRight.X - TopLeft.X + 1;
        public int Height => BottomRight.Y - TopLeft.Y + 1;
        public int CenterX => (TopLeft.X + BottomRight.X) / 2;

        // Returns true if this tile should be treated as open air for ground detection
        // (platforms, trees, cacti, sunflowers, and other surface plants).
        private const int MaxSupportBridgeDepth = 8;

        private static bool IsAirOrVegetation(Tile t)
        {
            if (!t.HasTile) return true;
            if (TileID.Sets.Platforms[t.TileType]) return true;
            switch (t.TileType)
            {
                case TileID.Trees:
                case TileID.PalmTree:
                case TileID.MushroomTrees:
                case TileID.VanityTreeSakura:
                case TileID.VanityTreeYellowWillow:
                case TileID.Cactus:
                case TileID.Sunflower:
                case TileID.Plants:
                case TileID.Plants2:
                case TileID.JunglePlants:
                case TileID.JunglePlants2:
                case TileID.MushroomPlants:
                case TileID.Vines:
                case TileID.JungleVines:
                    return true;
                default:
                    return false;
            }
        }

        // ── Ground sampling ───────────────────────────────────────────────────
        // Finds the Y of the actual world surface at column x by scanning downward
        // from the top of the world. Trees and vegetation are ignored so they don't
        // prevent the real ground tile from being found.
        public static int FindGroundY(int x, int startY = 0)
        {
            int searchStartY = System.Math.Max(10, startY);
            for (int y = searchStartY; y < Main.maxTilesY - 10; y++)
            {
                Tile t = Framing.GetTileSafely(x, y);
                if (IsAirOrVegetation(t)) continue;

                // Require 3 open-air-or-vegetation tiles directly above before accepting
                // as ground — filters floating islands that sit on their own solid base.
                bool hasAirAbove = true;
                for (int above = y - 1; above >= System.Math.Max(0, y - 3); above--)
                {
                    if (!IsAirOrVegetation(Framing.GetTileSafely(x, above)))
                    {
                        hasAirAbove = false;
                        break;
                    }
                }

                if (hasAirAbove)
                    return y;
            }
            return (int)Main.worldSurface;
        }

        private static int FindNearbySupportY(int x, int startY)
        {
            for (int y = startY; y <= startY + MaxSupportBridgeDepth && y < Main.maxTilesY - 10; y++)
            {
                if (!WorldGen.InWorld(x, y, 1))
                    break;

                Tile tile = Framing.GetTileSafely(x, y);
                if (IsSolidSupportTile(tile))
                    return y;
            }

            return -1;
        }

        // ── Biome fill ────────────────────────────────────────────────────────
        private static bool IsSolidSupportTile(Tile tile)
        {
            return tile.HasTile
                && !tile.IsActuated
                && Main.tileSolid[tile.TileType]
                && !TileID.Sets.Platforms[tile.TileType];
        }

        private static ushort ResolveSupportFillTileType(int x, int y)
        {
            if (TrySampleNearbyTerrainFillType(x, y, out ushort sampledTileType))
                return sampledTileType;

            return GuessFallbackFillTileType(x, y);
        }

        private static bool TrySampleNearbyTerrainFillType(int x, int y, out ushort fillTileType)
        {
            var scores = new Dictionary<ushort, int>();

            for (int dy = 0; dy <= MaxSupportBridgeDepth + 2; dy++)
            {
                for (int dx = -6; dx <= 6; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int sampleX = x + dx;
                    int sampleY = y + dy;
                    if (!WorldGen.InWorld(sampleX, sampleY, 1))
                        continue;

                    Tile sampleTile = Framing.GetTileSafely(sampleX, sampleY);
                    if (!IsSolidSupportTile(sampleTile))
                        continue;

                    if (!TryNormalizeTerrainFillTileType(sampleTile.TileType, out ushort normalizedTileType))
                        continue;

                    int weight = 40 - (dy * 4) - (Math.Abs(dx) * 3);
                    if (weight <= 0)
                        continue;

                    scores[normalizedTileType] = scores.TryGetValue(normalizedTileType, out int existingWeight)
                        ? existingWeight + weight
                        : weight;
                }
            }

            if (scores.Count > 0)
            {
                fillTileType = scores
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key)
                    .First()
                    .Key;
                return true;
            }

            fillTileType = 0;
            return false;
        }

        private static bool TryNormalizeTerrainFillTileType(ushort tileType, out ushort normalizedTileType)
        {
            switch (tileType)
            {
                case TileID.Dirt:
                case TileID.Mud:
                case TileID.Stone:
                case TileID.SnowBlock:
                case TileID.IceBlock:
                case TileID.BreakableIce:
                case TileID.CorruptIce:
                case TileID.HallowedIce:
                case TileID.FleshIce:
                case TileID.Sand:
                case TileID.Ebonsand:
                case TileID.Crimsand:
                case TileID.Pearlsand:
                case TileID.HardenedSand:
                case TileID.CorruptHardenedSand:
                case TileID.CrimsonHardenedSand:
                case TileID.HallowHardenedSand:
                case TileID.Sandstone:
                case TileID.CorruptSandstone:
                case TileID.CrimsonSandstone:
                case TileID.HallowSandstone:
                case TileID.Ebonstone:
                case TileID.Crimstone:
                case TileID.Pearlstone:
                case TileID.ClayBlock:
                case TileID.Silt:
                case TileID.Slush:
                case TileID.Marble:
                case TileID.Granite:
                case TileID.Hive:
                case TileID.Ash:
                    normalizedTileType = tileType;
                    return true;
                case TileID.Grass:
                case TileID.CorruptGrass:
                case TileID.CrimsonGrass:
                case TileID.HallowedGrass:
                case TileID.AshGrass:
                    normalizedTileType = TileID.Dirt;
                    return true;
                case TileID.JungleGrass:
                case TileID.MushroomGrass:
                case TileID.CorruptJungleGrass:
                case TileID.CrimsonJungleGrass:
                    normalizedTileType = TileID.Mud;
                    return true;
                default:
                    normalizedTileType = 0;
                    return false;
            }
        }

        // Returns a best-effort biome-appropriate fallback when nearby terrain
        // sampling doesn't provide a natural fill tile.
        private static ushort GuessFallbackFillTileType(int x, int y)
        {
            if (y >= Main.rockLayer)
                return TileID.Stone;

            bool likelySnow = x < Main.maxTilesX * 0.35f && y < (int)Main.worldSurface + 60;
            if (likelySnow)
                return y > Main.worldSurface ? TileID.IceBlock : TileID.SnowBlock;

            bool likelyDesert = x > Main.maxTilesX * 0.65f && y < (int)Main.worldSurface + 80;
            if (likelyDesert)
                return y > Main.worldSurface ? TileID.HardenedSand : TileID.Sand;

            return y > Main.worldSurface ? TileID.Stone : TileID.Dirt;
        }

        // ── Capture ───────────────────────────────────────────────────────────
        public static BuildingZone Capture(Point16 topLeft, Point16 bottomRight, int id)
        {
            var zone = new BuildingZone
            {
                Id          = id,
                TopLeft     = topLeft,
                BottomRight = bottomRight,
            };

            // Sample the ground reference at the center column by scanning downward
            // from just below the zone. This is the Y of the tile the structure sits on.
            int centerX = (topLeft.X + bottomRight.X) / 2;
            int capStart = bottomRight.Y + 1;
            zone.SavedGroundY = capStart;
            for (int y = capStart; y < capStart + 500 && y < Main.maxTilesY - 10; y++)
            {
                Tile t = Framing.GetTileSafely(centerX, y);
                if (t.HasTile && !TileID.Sets.Platforms[t.TileType])
                {
                    zone.SavedGroundY = y;
                    break;
                }
            }

            ModContent.GetInstance<DynamicWorlds>().Logger.Info(
                $"[SA] Captured zone #{id}: TL=({topLeft.X},{topLeft.Y}) BR=({bottomRight.X},{bottomRight.Y}) centerX={centerX} SavedGroundY={zone.SavedGroundY} worldSurface={Main.worldSurface:F0}");

            // Snapshot every tile in the bounding rectangle
            for (int x = topLeft.X; x <= bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y <= bottomRight.Y; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1)) continue;
                    var pos  = new Point16(x, y);
                    zone.Tiles[pos] = AnchoredTileData.CaptureFromWorld(x, y);
                }
            }

            // Capture chest contents for any container whose top-left tile is in the zone.
            // Chests are 2×2; we only need to check the top-left corner.
            for (int i = 0; i < Main.chest.Length; i++)
            {
                var ch = Main.chest[i];
                if (ch == null) continue;
                var cpos = new Point16(ch.x, ch.y);
                if (cpos.X >= topLeft.X && cpos.X <= bottomRight.X &&
                    cpos.Y >= topLeft.Y && cpos.Y <= bottomRight.Y)
                {
                    zone.Chests[cpos] = SavedChestContents.CaptureFromWorld(cpos);
                }
            }

            // If the local player's spawn (bed) is inside the zone, record it.
            Player p = Main.LocalPlayer;
            if (p != null && p.SpawnX >= topLeft.X && p.SpawnX <= bottomRight.X &&
                             p.SpawnY >= topLeft.Y && p.SpawnY <= bottomRight.Y)
            {
                zone.SavedSpawn = new Point16(p.SpawnX, p.SpawnY);
            }

            return zone;
        }

        public static BuildingZone CaptureConnected(Point16 selectionTopLeft, Point16 selectionBottomRight, IEnumerable<Point16> seedTiles, int id)
        {
            HashSet<Point16> connectedTiles = FindConnectedInterestingTiles(selectionTopLeft, selectionBottomRight, seedTiles);
            if (connectedTiles.Count == 0)
                return Capture(selectionTopLeft, selectionBottomRight, id);

            short minX = connectedTiles.Min(pos => pos.X);
            short maxX = connectedTiles.Max(pos => pos.X);
            short minY = connectedTiles.Min(pos => pos.Y);
            short maxY = connectedTiles.Max(pos => pos.Y);

            // Pad by 1 tile inside the selection to keep edge objects (doors, frames) that touch the boundary
            short paddedMinX = (short)Math.Max(selectionTopLeft.X, minX - 1);
            short paddedMaxX = (short)Math.Min(selectionBottomRight.X, maxX + 1);
            short paddedMinY = (short)Math.Max(selectionTopLeft.Y, minY - 1);
            short paddedMaxY = (short)Math.Min(selectionBottomRight.Y, maxY + 1);

            var topLeft = new Point16(paddedMinX, paddedMinY);
            var bottomRight = new Point16(paddedMaxX, paddedMaxY);
            var zone = new BuildingZone
            {
                Id = id,
                TopLeft = topLeft,
                BottomRight = bottomRight,
            };

            int centerX = (topLeft.X + bottomRight.X) / 2;
            int capStart = bottomRight.Y + 1;
            zone.SavedGroundY = capStart;
            for (int y = capStart; y < capStart + 500 && y < Main.maxTilesY - 10; y++)
            {
                Tile t = Framing.GetTileSafely(centerX, y);
                if (t.HasTile && !TileID.Sets.Platforms[t.TileType])
                {
                    zone.SavedGroundY = y;
                    break;
                }
            }

            ModContent.GetInstance<DynamicWorlds>().Logger.Info(
                $"[SA] Captured connected zone #{id}: TL=({topLeft.X},{topLeft.Y}) BR=({bottomRight.X},{bottomRight.Y}) seeds={connectedTiles.Count} centerX={centerX} SavedGroundY={zone.SavedGroundY} worldSurface={Main.worldSurface:F0}");

            foreach (Point16 pos in connectedTiles)
                zone.Tiles[pos] = AnchoredTileData.CaptureFromWorld(pos.X, pos.Y);

            for (int i = 0; i < Main.chest.Length; i++)
            {
                Chest chest = Main.chest[i];
                if (chest == null)
                    continue;

                var chestTopLeft = new Point16(chest.x, chest.y);
                if (connectedTiles.Contains(chestTopLeft))
                    zone.Chests[chestTopLeft] = SavedChestContents.CaptureFromWorld(chestTopLeft);
            }

            Player player = Main.LocalPlayer;
            if (player != null &&
                player.SpawnX >= topLeft.X && player.SpawnX <= bottomRight.X &&
                player.SpawnY >= topLeft.Y && player.SpawnY <= bottomRight.Y)
            {
                zone.SavedSpawn = new Point16(player.SpawnX, player.SpawnY);
            }

            return zone;
        }

        private static HashSet<Point16> FindConnectedInterestingTiles(Point16 selectionTopLeft, Point16 selectionBottomRight, IEnumerable<Point16> seedTiles)
        {
            var connected = new HashSet<Point16>();
            if (seedTiles == null)
                return connected;

            var queue = new Queue<Point16>();
            foreach (Point16 seed in seedTiles)
            {
                if (!IsWithin(selectionTopLeft, selectionBottomRight, seed) || !IsInteresting(seed))
                    continue;

                if (connected.Add(seed))
                    queue.Enqueue(seed);
            }

            Point16[] neighbors =
            {
                new Point16(1, 0),
                new Point16(-1, 0),
                new Point16(0, 1),
                new Point16(0, -1),
            };

            while (queue.Count > 0)
            {
                Point16 current = queue.Dequeue();
                foreach (Point16 neighborOffset in neighbors)
                {
                    var neighbor = new Point16(
                        (short)(current.X + neighborOffset.X),
                        (short)(current.Y + neighborOffset.Y));

                    if (!IsWithin(selectionTopLeft, selectionBottomRight, neighbor))
                        continue;

                    if (!IsInteresting(neighbor))
                        continue;

                    if (connected.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return connected;
        }

        private static bool IsWithin(Point16 topLeft, Point16 bottomRight, Point16 point)
        {
            return point.X >= topLeft.X && point.X <= bottomRight.X
                && point.Y >= topLeft.Y && point.Y <= bottomRight.Y;
        }

        private static bool IsInteresting(Point16 point)
        {
            if (!WorldGen.InWorld(point.X, point.Y, 1))
                return false;

            Tile tile = Framing.GetTileSafely(point.X, point.Y);
            return tile.HasTile
                || tile.WallType > 0
                || tile.LiquidAmount > 0
                || tile.RedWire
                || tile.BlueWire
                || tile.GreenWire
                || tile.YellowWire
                || tile.HasActuator
                || tile.IsActuated;
        }

        public ZoneRestorePlacement PredictRestorePlacement(int targetCenterX, int groundSearchStartY = 0)
        {
            int deltaX = targetCenterX - CenterX;
            int newGroundY = FindGroundY(targetCenterX, groundSearchStartY);

            // Place the zone so its bottom row sits one tile INTO the ground,
            // giving a natural embedded/planted look.
            int newBottomY = newGroundY;
            int deltaY = newBottomY - BottomRight.Y;

            return new ZoneRestorePlacement(
                new Point16((short)(TopLeft.X + deltaX), (short)(TopLeft.Y + deltaY)),
                new Point16((short)(BottomRight.X + deltaX), (short)newBottomY),
                deltaX,
                deltaY,
                newGroundY);
        }

        // ── Restore (translated) ──────────────────────────────────────────────
        // Called after worldgen. Finds new ground level at the same X column,
        // computes deltaY = newGroundY - savedGroundY, shifts every tile by deltaY,
        // fills gaps, then re-captures the zone at its new position so the next
        // regen uses up-to-date coordinates.
        public void RestoreToWorld()
        {
            RestoreToPlacement(PredictRestorePlacement(CenterX), "[SA]");
        }

        public void RestoreToPlacement(ZoneRestorePlacement placement, string logPrefix)
        {
            int centerX = CenterX + placement.DeltaX;
            int newGroundY = placement.GroundY;
            int deltaX = placement.DeltaX;
            int deltaY = placement.DeltaY;
            int newTopY = placement.TopLeft.Y;
            int newBottomY = placement.BottomRight.Y;
            int newLeftX = placement.TopLeft.X;
            int newRightX = placement.BottomRight.X;
            int lowestTouchedY = newBottomY;

            ModContent.GetInstance<DynamicWorlds>().Logger.Info(
                $"{logPrefix} RestoreZone #{Id}: centerX={centerX} SavedGroundY={SavedGroundY} newGroundY={newGroundY} delta=({deltaX},{deltaY}) TL=({TopLeft.X},{TopLeft.Y})->({newLeftX},{newTopY}) BR=({BottomRight.X},{BottomRight.Y})->({newRightX},{newBottomY}) worldSurface={Main.worldSurface:F0}");

            // 1. Bridge only small support gaps beneath the restored footprint.
            //    If the structure lands on a floating island, leave it floating
            //    instead of creating a dirt pillar down to the surface below.
            if (!placement.SkipSupportBridging)
            {
                for (int x = newLeftX; x <= newRightX; x++)
                {
                    int supportY = FindNearbySupportY(x, newBottomY + 1);
                    if (supportY <= newBottomY + 1)
                        continue;

                    for (int y = newBottomY + 1; y < supportY; y++)
                    {
                        if (!WorldGen.InWorld(x, y, 1))
                            continue;

                        Tile tile = Framing.GetTileSafely(x, y);
                        tile.ClearEverything();
                        tile.HasTile  = true;
                        tile.TileType = ResolveSupportFillTileType(x, y);
                        lowestTouchedY = Math.Max(lowestTouchedY, y);
                    }
                }
            }

            // 2. Clear only the exact footprint where building tiles will go.
            //    Do NOT clear above the building - this prevents filling dirt over it.
            //    Only clear the building's own footprint + a small area below for settling.
            if (deltaX != 0 || deltaY != 0)
            {
                for (int x = newLeftX; x <= newRightX; x++)
                {
                    for (int y = newTopY; y <= newBottomY; y++)
                    {
                        if (!WorldGen.InWorld(x, y, 1)) continue;
                        Framing.GetTileSafely(x, y).ClearEverything();
                    }
                }
            }

            // 3. Write all captured tiles at their translated Y positions.
            foreach (var kv in Tiles)
            {
                int nx = kv.Key.X + deltaX;
                int ny = kv.Key.Y + deltaY;
                if (!WorldGen.InWorld(nx, ny, 1)) continue;

                var data = kv.Value;
                Tile tile = Framing.GetTileSafely(nx, ny);
                tile.ClearEverything();
                tile.HasTile      = data.Active;
                tile.TileType     = data.TileType;
                tile.TileFrameX   = data.FrameX;
                tile.TileFrameY   = data.FrameY;
                tile.WallType     = data.WallType;
                tile.LiquidAmount = data.Liquid;
                tile.LiquidType   = data.LiquidType;
                tile.IsHalfBlock  = data.HalfBlock;
                tile.Slope        = data.Slope;
                tile.RedWire      = data.WireRed;
                tile.BlueWire     = data.WireBlue;
                tile.GreenWire    = data.WireGreen;
                tile.YellowWire   = data.WireYellow;
                tile.HasActuator  = data.HasActuator;
                tile.IsActuated   = data.IsActuated;
            }

            // 4. Don't fill terrain around the building - let it settle naturally.
            //    The restored tiles will anchor the terrain, and WorldGen.RangeFrame 
            //    will handle framing. Manual filling can cause dirt to cover the building.

            // 5. Restore chest contents at translated positions.
            foreach (var kv in Chests)
            {
                int nx = kv.Key.X + deltaX;
                int ny = kv.Key.Y + deltaY;
                if (!WorldGen.InWorld(nx, ny, 1)) continue;

                int idx = Chest.FindChest(nx, ny);
                if (idx < 0)
                    idx = Chest.CreateChest(nx, ny);
                if (idx < 0 || Main.chest[idx] == null) continue;

                for (int i = 0; i < Chest.maxItems; i++)
                {
                    Main.chest[idx].item[i] = (kv.Value.Items[i] != null)
                        ? kv.Value.Items[i].Clone()
                        : new Item();
                }
            }

            // 6. Translate the player's spawn point if it was inside this zone.
            if (SavedSpawn.X >= 0 && SavedSpawn.Y >= 0)
            {
                Player p = Main.LocalPlayer;
                if (p != null)
                {
                    p.SpawnX = SavedSpawn.X + deltaX;
                    p.SpawnY = SavedSpawn.Y + deltaY;
                }
            }

            WorldGen.RangeFrame(
                System.Math.Max(0, newLeftX - 2),
                System.Math.Max(0, newTopY - 2),
                System.Math.Min(Main.maxTilesX, newRightX + 2),
                System.Math.Min(Main.maxTilesY, lowestTouchedY + 2));

            // 7. Update zone metadata to the new position so the next regen is correct.
            //    Always update — even if deltaY==0, SavedGroundY may have changed.
            {
                var newTl = new Point16((short)newLeftX, (short)newTopY);
                var newBr = new Point16((short)newRightX, (short)newBottomY);

                // Rebuild tile snapshot at new position
                var newTiles = new Dictionary<Point16, AnchoredTileData>();
                foreach (var kv in Tiles)
                {
                    int x = kv.Key.X + deltaX;
                    int y = kv.Key.Y + deltaY;
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    var newPos = new Point16((short)x, (short)y);
                    newTiles[newPos] = AnchoredTileData.CaptureFromWorld(x, y);
                }

                // Rebuild chest refs at new positions
                var newChests = new Dictionary<Point16, SavedChestContents>();
                foreach (var kv in Chests)
                {
                    var newPos = new Point16((short)(kv.Key.X + deltaX), (short)(kv.Key.Y + deltaY));
                    newChests[newPos] = new SavedChestContents { Position = newPos, Items = kv.Value.Items };
                }

                TopLeft      = newTl;
                BottomRight  = newBr;
                SavedGroundY = newGroundY;
                Tiles        = newTiles;
                Chests       = newChests;

                if (SavedSpawn.X >= 0)
                    SavedSpawn = new Point16((short)(SavedSpawn.X + deltaX), (short)(SavedSpawn.Y + deltaY));
            }
        }

        // ── Serialization ─────────────────────────────────────────────────────
        public TagCompound ToTag()
        {
            var tileTags = new List<TagCompound>();
            foreach (var kv in Tiles)
                tileTags.Add(kv.Value.ToTag());

            var chestTags = new List<TagCompound>();
            foreach (var kv in Chests)
                chestTags.Add(kv.Value.ToTag());

            return new TagCompound
            {
                ["id"]      = Id,
                ["tlx"]     = TopLeft.X,
                ["tly"]     = TopLeft.Y,
                ["brx"]     = BottomRight.X,
                ["bry"]     = BottomRight.Y,
                ["gndY"]    = SavedGroundY,
                ["tiles"]   = tileTags,
                ["chests"]  = chestTags,
                ["spawnX"]  = (int)SavedSpawn.X,
                ["spawnY"]  = (int)SavedSpawn.Y,
            };
        }

        public static BuildingZone FromTag(TagCompound tag)
        {
            var zone = new BuildingZone
            {
                Id           = tag.GetInt("id"),
                TopLeft      = new Point16(tag.GetShort("tlx"), tag.GetShort("tly")),
                BottomRight  = new Point16(tag.GetShort("brx"), tag.GetShort("bry")),
                SavedGroundY = tag.GetInt("gndY"),
                SavedSpawn   = new Point16(
                    tag.ContainsKey("spawnX") ? (short)tag.GetInt("spawnX") : (short)-1,
                    tag.ContainsKey("spawnY") ? (short)tag.GetInt("spawnY") : (short)-1),
            };

            var list = tag.GetList<TagCompound>("tiles");
            foreach (var t in list)
            {
                var data = AnchoredTileData.FromTag(t);
                zone.Tiles[data.Position] = data;
            }

            if (tag.ContainsKey("chests"))
            {
                var chestList = tag.GetList<TagCompound>("chests");
                foreach (var t in chestList)
                {
                    var c = SavedChestContents.FromTag(t);
                    zone.Chests[c.Position] = c;
                }
            }

            return zone;
        }
    }

    // -------------------------------------------------------------------------
    //  World-level system: holds all registered structure zones.
    // -------------------------------------------------------------------------
    public readonly struct ZoneRestorePlacement
    {
        public readonly Point16 TopLeft;
        public readonly Point16 BottomRight;
        public readonly short DeltaX;
        public readonly short DeltaY;
        public readonly int GroundY;
        public readonly bool SkipSupportBridging;

        public ZoneRestorePlacement(Point16 topLeft, Point16 bottomRight, int deltaX, int deltaY, int groundY, bool skipSupportBridging = false)
        {
            TopLeft = topLeft;
            BottomRight = bottomRight;
            DeltaX = (short)deltaX;
            DeltaY = (short)deltaY;
            GroundY = groundY;
            SkipSupportBridging = skipSupportBridging;
        }
    }

    public readonly struct RestoredZoneTransform
    {
        public readonly Point16 OriginalTopLeft;
        public readonly Point16 OriginalBottomRight;
        public readonly short DeltaX;
        public readonly short DeltaY;

        public RestoredZoneTransform(Point16 originalTopLeft, Point16 originalBottomRight, int deltaX, int deltaY)
        {
            OriginalTopLeft = originalTopLeft;
            OriginalBottomRight = originalBottomRight;
            DeltaX = (short)deltaX;
            DeltaY = (short)deltaY;
        }

        public bool Contains(Point16 point) =>
            point.X >= OriginalTopLeft.X && point.X <= OriginalBottomRight.X &&
            point.Y >= OriginalTopLeft.Y && point.Y <= OriginalBottomRight.Y;

        public Point16 Translate(Point16 point) =>
            new Point16((short)(point.X + DeltaX), (short)(point.Y + DeltaY));
    }

    public class StructureAnchorSystem : ModSystem
    {
        // Keyed by zone Id
        public static readonly Dictionary<int, BuildingZone> Zones = new();
        private static readonly List<RestoredZoneTransform> LastRestoreTransforms = new();

        // Overlay texture drawn on zone tiles while the item is held
        public static Asset<Texture2D> ZoneIcon;

        private static int _nextId = 1;
        public static int NextId() => _nextId++;
        public static void RecalculateNextId() => _nextId = Zones.Count == 0 ? 1 : Zones.Keys.Max() + 1;

        public override void OnWorldLoad()
        {
            Zones.Clear();
            LastRestoreTransforms.Clear();
            _nextId = 1;

            if (ZoneIcon == null || !ZoneIcon.IsLoaded)
                ZoneIcon = ModContent.Request<Texture2D>("DynamicWorlds/Preservation/AnchoredTile");
        }

        public override void OnWorldUnload()
        {
            Zones.Clear();
            LastRestoreTransforms.Clear();
        }

        // Refresh the snapshot of chest contents in all structure zones.
        // Call this immediately before worldgen to capture any new items added to zone chests.
        public static void RefreshAllChestSnapshots()
        {
            foreach (var zone in Zones.Values)
            {
                // For each chest position in the zone, recapture its current contents
                var topLeftsToRefresh = new List<Point16>(zone.Chests.Keys);
                foreach (var chestPos in topLeftsToRefresh)
                {
                    zone.Chests[chestPos] = SavedChestContents.CaptureFromWorld(chestPos);
                }
            }
        }

        // Called during regen, after erased tiles are cleared and before regular anchors.
        public static void RestoreAllZones(bool announce = true)
        {
            LastRestoreTransforms.Clear();
            if (Zones.Count == 0) return;

            foreach (var kv in Zones)
            {
                Point16 oldTopLeft = kv.Value.TopLeft;
                Point16 oldBottomRight = kv.Value.BottomRight;

                kv.Value.RestoreToWorld();
                LastRestoreTransforms.Add(new RestoredZoneTransform(
                    oldTopLeft,
                    oldBottomRight,
                    kv.Value.TopLeft.X - oldTopLeft.X,
                    kv.Value.TopLeft.Y - oldTopLeft.Y));
            }

            int restoredPylons = PylonRestoreHelper.RestoreVanillaPylons(
                Zones.Values.SelectMany(zone => zone.Tiles.Keys));
            if (restoredPylons > 0)
                ModContent.GetInstance<DynamicWorlds>().Logger.Info($"[StructureAnchor] Re-registered {restoredPylons} restored vanilla pylon(s).");

            if (announce && Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText($"Restored {Zones.Count} structure zone{(Zones.Count == 1 ? "" : "s")}.", 180, 220, 255);
        }

        public static bool TryTranslateSavedPoint(Point16 savedPoint, out Point16 translatedPoint)
        {
            foreach (var transform in LastRestoreTransforms)
            {
                if (transform.Contains(savedPoint))
                {
                    translatedPoint = transform.Translate(savedPoint);
                    return true;
                }
            }

            translatedPoint = default;
            return false;
        }

        public static bool TryGetZoneAt(Point16 tilePosition, out int zoneId)
        {
            foreach (var kv in Zones)
            {
                BuildingZone zone = kv.Value;
                if (tilePosition.X >= zone.TopLeft.X && tilePosition.X <= zone.BottomRight.X &&
                    tilePosition.Y >= zone.TopLeft.Y && tilePosition.Y <= zone.BottomRight.Y)
                {
                    zoneId = kv.Key;
                    return true;
                }
            }

            zoneId = -1;
            return false;
        }

        public static bool TryFindOverlappingAnchoredTile(Point16 topLeft, Point16 bottomRight, out Point16 overlapTile)
        {
            foreach (Point16 tilePosition in AnchoredTileSystem.AnchoredTiles.Keys)
            {
                if (tilePosition.X >= topLeft.X && tilePosition.X <= bottomRight.X &&
                    tilePosition.Y >= topLeft.Y && tilePosition.Y <= bottomRight.Y)
                {
                    overlapTile = tilePosition;
                    return true;
                }
            }

            overlapTile = default;
            return false;
        }

        internal static bool TryCreateZone(Point16 topLeft, Point16 bottomRight, out BuildingZone zone, out string message)
        {
            zone = null;

            if (bottomRight.X - topLeft.X < 1 || bottomRight.Y - topLeft.Y < 1)
            {
                message = "Drag a larger area to define a structure zone.";
                return false;
            }

            foreach (var kv in Zones)
            {
                BuildingZone existingZone = kv.Value;
                bool overlapX = topLeft.X <= existingZone.BottomRight.X && bottomRight.X >= existingZone.TopLeft.X;
                bool overlapY = topLeft.Y <= existingZone.BottomRight.Y && bottomRight.Y >= existingZone.TopLeft.Y;
                if (overlapX && overlapY)
                {
                    message = $"Zone overlaps with existing zone #{kv.Key} — tiles can only belong to one zone.";
                    return false;
                }
            }

            if (TryFindOverlappingAnchoredTile(topLeft, bottomRight, out Point16 anchoredOverlap))
            {
                message =
                    $"Structure zones cannot overlap individually anchored tiles. Remove the anchor at ({anchoredOverlap.X}, {anchoredOverlap.Y}) first.";
                return false;
            }

            int newId = NextId();
            zone = BuildingZone.Capture(topLeft, bottomRight, newId);
            Zones[newId] = zone;

            int area = zone.Width * zone.Height;
            message = $"Structure zone #{newId} created: {zone.Width}×{zone.Height} ({area} tiles). Ground ref Y={zone.SavedGroundY}.";
            return true;
        }

        internal static bool RemoveZoneAt(Point16 clickPos, out int removedZoneId, out string message)
        {
            foreach (var kv in Zones)
            {
                BuildingZone zone = kv.Value;
                bool insideX = clickPos.X >= zone.TopLeft.X && clickPos.X <= zone.BottomRight.X;
                bool insideY = clickPos.Y >= zone.TopLeft.Y && clickPos.Y <= zone.BottomRight.Y;
                if (!insideX || !insideY)
                    continue;

                removedZoneId = kv.Key;
                Zones.Remove(removedZoneId);
                message = $"Structure zone #{removedZoneId} removed. ({Zones.Count} zones remain)";
                return true;
            }

            removedZoneId = -1;
            message = "Shift+Click on a structure zone to remove it.";
            return false;
        }

        internal static bool RemoveZoneById(int zoneId, out string message)
        {
            if (Zones.Remove(zoneId))
            {
                message = $"Structure zone #{zoneId} removed. ({Zones.Count} zones remain)";
                return true;
            }

            message = $"Zone #{zoneId} not found.";
            return false;
        }

        internal static int ClearAllZones()
        {
            int count = Zones.Count;
            Zones.Clear();
            return count;
        }

        internal static void UpsertSyncedZone(BuildingZone syncedZone)
        {
            if (syncedZone == null)
                return;

            if (Zones.TryGetValue(syncedZone.Id, out BuildingZone existingZone) && existingZone.Tiles.Count > 0)
                return;

            Zones[syncedZone.Id] = syncedZone;
        }

        internal static void RemoveSyncedZone(int zoneId)
        {
            Zones.Remove(zoneId);
        }

        // ── Save / Load ───────────────────────────────────────────────────────
        public override void SaveWorldData(TagCompound tag)
        {
            var list = new List<TagCompound>();
            foreach (var kv in Zones)
                list.Add(kv.Value.ToTag());
            tag["BuildingZones"]  = list;
            tag["BuildingNextId"] = _nextId;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Zones.Clear();
            _nextId = 1;

            if (tag.ContainsKey("BuildingZones"))
            {
                var list = tag.GetList<TagCompound>("BuildingZones");
                foreach (var t in list)
                {
                    var zone = BuildingZone.FromTag(t);
                    Zones[zone.Id] = zone;
                }
            }

            if (tag.ContainsKey("BuildingNextId"))
                _nextId = tag.GetInt("BuildingNextId");

            if (_nextId <= 0 || _nextId <= Zones.Keys.DefaultIfEmpty(0).Max())
                RecalculateNextId();
        }

        // ── Visual overlay ────────────────────────────────────────────────────
        public override void PostDrawTiles()
        {
            Player player = Main.LocalPlayer;
            if (player?.HeldItem == null) return;
            if (!WorldToolOverlayHelper.IsHoldingWorldTool(player)) return;

            SpriteBatch sb       = Main.spriteBatch;
            Vector2     screenPos = Main.screenPosition;

            WorldToolOverlayHelper.BeginOverlay(sb);

            // Draw all registered zones
            foreach (var kv in Zones)
            {
                var zone = kv.Value;
                WorldToolOverlayHelper.DrawAreaOverlay(sb, zone.TopLeft, zone.BottomRight, screenPos,
                    new Color(100, 180, 255) * 0.35f, Color.DeepSkyBlue);
            }

            // Draw in-progress drag preview for the item being held
            var mp = player.GetModPlayer<StructureAnchorPlayer>();
            if (mp.IsDragging)
            {
                int x0 = System.Math.Min(mp.DragStart.X, mp.DragEnd.X);
                int x1 = System.Math.Max(mp.DragStart.X, mp.DragEnd.X);
                int y0 = System.Math.Min(mp.DragStart.Y, mp.DragEnd.Y);
                int y1 = System.Math.Max(mp.DragStart.Y, mp.DragEnd.Y);
                WorldToolOverlayHelper.DrawAreaOverlay(sb,
                    new Point16(x0, y0), new Point16(x1, y1), screenPos,
                    Color.Gold * 0.3f, Color.Gold);
            }

            sb.End();
        }

    }

    // -------------------------------------------------------------------------
    //  ModPlayer: tracks drag state and the zone assigned to this item instance.
    //  Each StructureAnchorItem stores its own zone id in the item's tag.
    // -------------------------------------------------------------------------
    public class StructureAnchorPlayer : ModPlayer
    {
        public bool    IsDragging = false;
        public Point16 DragStart;
        public Point16 DragEnd;

        private bool _wasHoldingLastFrame = false;

        public override void PostUpdate()
        {
            if (Main.netMode == NetmodeID.Server || Main.mapFullscreen || Player.whoAmI != Main.myPlayer)
            {
                if (IsDragging) CancelDrag();
                _wasHoldingLastFrame = false;
                return;
            }

            bool holding = Player.HeldItem?.type == ModContent.ItemType<StructureAnchorItem>();
            if (!holding)
            {
                if (IsDragging) CancelDrag();
                _wasHoldingLastFrame = false;
                return;
            }

            bool mouseHeld = Main.mouseLeft && !Main.LocalPlayer.mouseInterface;
            bool shiftHeld = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || 
                            Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
            int  tx = (int)(Main.MouseWorld.X / 16f);
            int  ty = (int)(Main.MouseWorld.Y / 16f);

            // Shift+Click: Select and remove a specific zone from the world
            if (shiftHeld && Main.mouseLeft && !_wasHoldingLastFrame && !Main.LocalPlayer.mouseInterface)
            {
                RemoveZoneAtPosition(new Point16(tx, ty));
                _wasHoldingLastFrame = true;
                return;
            }

            // Normal drag: Create a new zone
            if (mouseHeld)
            {
                if (!_wasHoldingLastFrame)
                {
                    IsDragging = true;
                    DragStart  = new Point16(tx, ty);
                    DragEnd    = DragStart;
                }
                else if (IsDragging)
                {
                    DragEnd = new Point16(tx, ty);
                }
            }
            else if (_wasHoldingLastFrame && IsDragging)
            {
                SoundEngine.PlaySound(SoundID.Item4, Player.position);
                IsDragging = false;
                CommitZone();
            }

            _wasHoldingLastFrame = mouseHeld;
        }

        private void CommitZone()
        {
            int x0 = System.Math.Min(DragStart.X, DragEnd.X);
            int x1 = System.Math.Max(DragStart.X, DragEnd.X);
            int y0 = System.Math.Min(DragStart.Y, DragEnd.Y);
            int y1 = System.Math.Max(DragStart.Y, DragEnd.Y);

            var tl = new Point16(x0, y0);
            var br = new Point16(x1, y1);

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                if (StructureAnchorSystem.TryCreateZone(tl, br, out _, out string message))
                    Main.NewText(message, 100, 200, 255);
                else
                    Main.NewText(message, 255, 120, 120);

                return;
            }

            DynamicWorldsNet.RequestStructureZoneCreate(tl, br);
        }

        private void RemoveZoneAtPosition(Point16 clickPos)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                if (StructureAnchorSystem.RemoveZoneAt(clickPos, out _, out string message))
                {
                    SoundEngine.PlaySound(SoundID.Item14, Player.position);
                    Main.NewText(message, 255, 150, 100);
                }
                else
                {
                    Main.NewText(message, 255, 200, 80);
                }

                return;
            }

            DynamicWorldsNet.RequestStructureZoneRemoveAt(clickPos);
        }

        public void CancelDrag()
        {
            IsDragging           = false;
            _wasHoldingLastFrame = false;
        }
    }

    // -------------------------------------------------------------------------
    //  The Structure Anchor item. Now purely a tool for creating/editing zones.
    //  Zones are owned by the world, not by individual items.
    // -------------------------------------------------------------------------
    public class StructureAnchorItem : ModItem
    {
        public override string Texture => "DynamicWorlds/Preservation/StructureAnchorItem";

        public override void SetDefaults()
        {
            Item.width        = 32;
            Item.height       = 32;
            Item.useStyle     = ItemUseStyleID.Shoot;
            Item.useTime      = 12;
            Item.useAnimation = 18;
            Item.rare         = ItemRarityID.LightRed;
            Item.value        = Item.buyPrice(gold: 1);
            Item.maxStack     = 1;
            Item.consumable   = false;
            Item.noMelee      = true;
            Item.noUseGraphic = false;
            Item.UseSound     = SoundID.Item8;
        }

        public override bool CanUseItem(Player player) => true;
        public override bool ConsumeItem(Player player) => false;

        public override void ModifyTooltips(System.Collections.Generic.List<Terraria.ModLoader.TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "BAInfo1",
                "Left-click and drag to project structure zones.")
                { OverrideColor = Color.LimeGreen });
            tooltips.Add(new TooltipLine(Mod, "BAInfo2",
                "Shift+Click inside a structure zone to remove it.")
                { OverrideColor = Color.LightBlue });
            tooltips.Add(new TooltipLine(Mod, "BAInfo3",
                "Zones are saved to the world and persist through resets.")
                { OverrideColor = Color.Gray });
            tooltips.Add(new TooltipLine(Mod, "BAInfo4",
                "Structure zones cannot overlap individually anchored tiles.")
                { OverrideColor = Color.Orange });
            tooltips.Add(new TooltipLine(Mod, "BAInfo5",
                "Hold any world tool to see anchors, erasures, structure zones, and Biome Dowser zones.")
                { OverrideColor = Color.LightSkyBlue });
            
            int zoneCount = StructureAnchorSystem.Zones.Count;
            if (zoneCount > 0)
            {
                tooltips.Add(new TooltipLine(Mod, "BAZoneCount",
                    $"World has {zoneCount} structure zone{(zoneCount == 1 ? "" : "s")}")
                    { OverrideColor = Color.DeepSkyBlue });
            }
        }
    }
}
